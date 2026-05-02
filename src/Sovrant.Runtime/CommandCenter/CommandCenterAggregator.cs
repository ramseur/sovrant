using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Missions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.CommandCenter;

/// <summary>
/// Phase 90 / Phase 89 MVP — read-only aggregator that flattens
/// "what is the engine doing right now?" across four sources:
/// missions, team runs, agent runs, and chat sessions.
/// Pure-read; never mutates state. The cockpit polls this every ~2s.
/// </summary>
public sealed class CommandCenterAggregator
{
    private readonly IMissionStore _missions;
    private readonly IAgentRunStore _runs;
    private readonly ISessionStore _sessions;
    private readonly ILogger<CommandCenterAggregator> _logger;

    public CommandCenterAggregator(
        IMissionStore missions,
        IAgentRunStore runs,
        ISessionStore sessions,
        ILogger<CommandCenterAggregator> logger)
    {
        _missions = missions;
        _runs = runs;
        _sessions = sessions;
        _logger = logger;
    }

    /// <summary>
    /// Returns the current cockpit state — every active mission, team run,
    /// agent run, and recent chat session. "Active" means in flight; we also
    /// surface the top few recently-ended rows per source so the cockpit
    /// shows continuity instead of going empty between turns.
    /// </summary>
    public async Task<CommandCenterState> GetActiveStateAsync(
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        var rows = new List<CommandCenterRow>();
        var now = DateTimeOffset.UtcNow;

        var missionsSnapshot = await SafeListMissionsAsync(ownerUserId, ct).ConfigureAwait(false);
        var activeMissions = 0;
        foreach (var m in missionsSnapshot)
        {
            var isActive = m.Status is MissionStatus.Planning
                                    or MissionStatus.Running
                                    or MissionStatus.AwaitingHuman;
            if (isActive) activeMissions++;
            else if (m.UpdatedAt < now.AddMinutes(-15)) continue;

            rows.Add(new CommandCenterRow(
                Kind: "mission",
                Id: m.Id,
                Title: Truncate(m.Goal, 120),
                Status: m.Status.ToString(),
                StartedAt: m.CreatedAt,
                LastActivity: m.UpdatedAt,
                OwnerLabel: m.OwnerUserId,
                Preview: null,
                CostUsd: null,
                DetailRoute: $"/missions/{m.Id}",
                WorkspaceId: m.WorkspaceId,
                ProjectId: m.ProjectId));
        }

        var runsSnapshot = await SafeListRunsAsync(ownerUserId, ct).ConfigureAwait(false);
        var activeTeamRuns = 0;
        var activeAgentRuns = 0;
        foreach (var r in runsSnapshot)
        {
            var isActive = string.Equals(r.Status, "running", StringComparison.OrdinalIgnoreCase)
                        || r.EndedAt is null;
            if (isActive)
            {
                if (!string.IsNullOrEmpty(r.TeamId)) activeTeamRuns++;
                else activeAgentRuns++;
            }
            else if ((r.EndedAt ?? r.StartedAt) < now.AddMinutes(-15)) continue;

            var kind = !string.IsNullOrEmpty(r.TeamId) ? "team-run" : "agent-run";
            var title = !string.IsNullOrEmpty(r.MemberId)
                ? $"{r.Kind} · {r.MemberId}"
                : r.Kind;

            rows.Add(new CommandCenterRow(
                Kind: kind,
                Id: r.RunId,
                Title: title,
                Status: r.Status,
                StartedAt: r.StartedAt,
                LastActivity: r.EndedAt ?? r.StartedAt,
                OwnerLabel: r.UserId,
                Preview: null,
                CostUsd: r.CostUsd,
                DetailRoute: !string.IsNullOrEmpty(r.TeamId) ? "/orchestration" : "/activity",
                WorkspaceId: r.WorkspaceId,
                ProjectId: r.ProjectId));
        }

        var sessionRows = await SafeRecentSessionsAsync(ownerUserId, ct).ConfigureAwait(false);
        rows.AddRange(sessionRows);

        var ordered = rows
            .OrderByDescending(r => r.LastActivity)
            .Take(50)
            .ToList();

        return new CommandCenterState(
            GeneratedAt: now,
            ActiveMissions: activeMissions,
            ActiveTeamRuns: activeTeamRuns,
            ActiveAgentRuns: activeAgentRuns,
            ActiveSessions: sessionRows.Count,
            Rows: ordered);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Cockpit aggregator must degrade gracefully if any one source fails — partial state is preferable to an exception propagating to the UI poll loop.")]
    private async Task<IReadOnlyList<Mission>> SafeListMissionsAsync(string? ownerUserId, CancellationToken ct)
    {
        try { return await _missions.ListAsync(ownerUserId, status: null, limit: 50, ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            LogSourceFailed(_logger, "missions", ex.Message);
            return [];
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Cockpit aggregator must degrade gracefully if any one source fails — partial state is preferable to an exception propagating to the UI poll loop.")]
    private async Task<IReadOnlyList<AgentRunRecord>> SafeListRunsAsync(string? ownerUserId, CancellationToken ct)
    {
        try
        {
            var filter = ownerUserId is null ? null : new AgentRunFilter(UserId: ownerUserId);
            return await _runs.ListAsync(filter, limit: 50, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogSourceFailed(_logger, "agent_runs", ex.Message);
            return [];
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Cockpit aggregator must degrade gracefully if any one source fails — partial state is preferable to an exception propagating to the UI poll loop.")]
    private async Task<List<CommandCenterRow>> SafeRecentSessionsAsync(string? ownerUserId, CancellationToken ct)
    {
        try
        {
            var ids = await _sessions.ListAsync(ownerUserId, ct).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var rows = new List<CommandCenterRow>(Math.Min(ids.Count, 10));

            foreach (var id in ids.Take(10))
            {
                IReadOnlyList<SessionEntry> entries;
                try { entries = await _sessions.LoadAsync(id, ownerUserId, ct).ConfigureAwait(false); }
                catch (Exception) { continue; }
                if (entries.Count == 0) continue;

                var first = entries[0];
                var last = entries[^1];
                if (last.Timestamp < now.AddHours(-2)) continue;

                var firstUser = entries.FirstOrDefault(e => e.Role == "user");
                var title = Truncate(firstUser?.Content ?? id, 80);
                var status = (now - last.Timestamp) < TimeSpan.FromMinutes(2) ? "Active" : "Recent";

                rows.Add(new CommandCenterRow(
                    Kind: "session",
                    Id: id,
                    Title: title,
                    Status: status,
                    StartedAt: first.Timestamp,
                    LastActivity: last.Timestamp,
                    OwnerLabel: ownerUserId,
                    Preview: Truncate(last.Content, 160),
                    CostUsd: null,
                    DetailRoute: "/activity"));
            }
            return rows;
        }
        catch (Exception ex)
        {
            LogSourceFailed(_logger, "sessions", ex.Message);
            return [];
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : string.Concat(s.AsSpan(0, max - 1), "…");
    }

    private static readonly Action<ILogger, string, string, Exception?> _logSourceFailed =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(1, "CommandCenterSourceFailed"),
            "Command Center source {Source} failed: {Error}");
    private static void LogSourceFailed(ILogger logger, string source, string error) =>
        _logSourceFailed(logger, source, error, null);
}
