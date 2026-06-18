using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.CommandCenter;
using Sovrant.Runtime.Missions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.UserDashboard;

/// <summary>
/// Phase 98 — read-only aggregator for the User Dashboard. Mirrors
/// <see cref="CommandCenterAggregator"/> but applies the per-user
/// visibility rule:
/// <para>
/// For each source (missions, agent runs incl. team runs, sessions), keep a
/// record when <c>UserId == userId</c> OR
/// (<c>!IsPrivate</c> AND the record's <c>WorkspaceId</c> is one the user
/// belongs to). Other users' private rows are excluded entirely — no masked
/// placeholders; that pattern remains in Command Center (admin-only).
/// </para>
/// <para>
/// Claws are surfaced as a section but unfiltered today —
/// <see cref="ClawConnectionInfo"/> carries no ownership fields yet (Phase 90
/// claw runtime is still landing). When the claw runtime gains user/workspace
/// scoping, revisit this aggregator to apply the same rule to claws.
/// </para>
/// </summary>
public sealed class UserDashboardAggregator
{
    private readonly IMissionStore _missions;
    private readonly IAgentRunStore _runs;
    private readonly ISessionStore _sessions;
    private readonly IClawConnectionMonitor _claws;
    private readonly IWorkspaceService _workspaces;
    private readonly IUserService? _users;
    private readonly ILogger<UserDashboardAggregator> _logger;

    public UserDashboardAggregator(
        IMissionStore missions,
        IAgentRunStore runs,
        ISessionStore sessions,
        IWorkspaceService workspaces,
        ILogger<UserDashboardAggregator> logger,
        IClawConnectionMonitor? claws = null,
        IUserService? users = null)
    {
        _missions = missions;
        _runs = runs;
        _sessions = sessions;
        _workspaces = workspaces;
        _claws = claws ?? NullClawConnectionMonitor.Instance;
        _users = users;
        _logger = logger;
    }

    /// <summary>
    /// Returns the user's personal cross-workspace dashboard state.
    /// </summary>
    public async Task<UserDashboardState> GetStateAsync(
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var now = DateTimeOffset.UtcNow;
        var workspaceIds = await SafeListWorkspaceIdsAsync(userId, ct).ConfigureAwait(false);

        var rows = new List<UserDashboardRow>();
        var ownMissions = 0;
        var ownTeamRuns = 0;
        var ownAgentRuns = 0;
        var ownSessions = 0;
        var othersPublic = 0;

        // ── Missions ───────────────────────────────────────────────────────
        var missionsSnapshot = await SafeListMissionsAsync(ct).ConfigureAwait(false);
        foreach (var m in missionsSnapshot)
        {
            if (!IsVisible(m.OwnerUserId, m.WorkspaceId, m.IsPrivate, userId, workspaceIds))
                continue;

            var isOwn = string.Equals(m.OwnerUserId, userId, StringComparison.Ordinal);
            // Own missions: show full history. Others' public: 30-day window (skip inactive old items).
            if (!isOwn && m.UpdatedAt < now.AddDays(-30)
                && m.Status is not (MissionStatus.Planning or MissionStatus.Running or MissionStatus.AwaitingHuman))
                continue;
            if (isOwn) ownMissions++; else othersPublic++;

            var stepCount = TryCountPlanSteps(m.PlanJson);
            var title = stepCount > 0
                ? $"{Truncate(m.Goal, 100)} · {stepCount} step{(stepCount == 1 ? "" : "s")}"
                : Truncate(m.Goal, 120);

            rows.Add(new UserDashboardRow(
                Kind: "mission",
                Id: m.Id,
                Title: title,
                Status: m.Status.ToString(),
                StartedAt: m.CreatedAt,
                LastActivity: m.UpdatedAt,
                OwnerLabel: m.OwnerUserId,
                Preview: null,
                CostUsd: null,
                DetailRoute: $"/missions/{m.Id}",
                WorkspaceId: m.WorkspaceId,
                ProjectId: m.ProjectId,
                IsOwn: isOwn,
                IsPrivate: m.IsPrivate));
        }

        // ── Agent runs (incl. team runs via TeamId) ────────────────────────
        var runsSnapshot = await SafeListRunsAsync(ct).ConfigureAwait(false);

        var childrenByParent = runsSnapshot
            .Where(r => !string.IsNullOrEmpty(r.ParentRunId))
            .GroupBy(r => r.ParentRunId!, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => new ChildRunStats(
                    Total: g.Count(),
                    Active: g.Count(c => c.EndedAt is null
                        || string.Equals(c.Status, "running", StringComparison.OrdinalIgnoreCase))),
                StringComparer.Ordinal);

        foreach (var r in runsSnapshot)
        {
            if (!IsVisible(r.UserId, r.WorkspaceId, r.IsPrivate, userId, workspaceIds))
                continue;

            var isOwn = string.Equals(r.UserId, userId, StringComparison.Ordinal);
            // Own runs: show full history. Others' public: 30-day window.
            if (!isOwn && (r.EndedAt ?? r.StartedAt) < now.AddDays(-30)
                && !string.Equals(r.Status, "running", StringComparison.OrdinalIgnoreCase))
                continue;
            var isTeamRun = !string.IsNullOrEmpty(r.TeamId) && string.IsNullOrEmpty(r.ParentRunId);
            if (isOwn)
            {
                if (isTeamRun) ownTeamRuns++; else ownAgentRuns++;
            }
            else othersPublic++;

            var kind = !string.IsNullOrEmpty(r.TeamId) ? "team-run" : "agent-run";
            var baseTitle = !string.IsNullOrEmpty(r.MemberId)
                ? $"{r.Kind} · {r.MemberId}"
                : r.Kind;

            string title;
            if (isTeamRun && childrenByParent.TryGetValue(r.RunId, out var stats))
            {
                title = stats.Active > 0
                    ? $"{baseTitle} · {stats.Total} member{(stats.Total == 1 ? "" : "s")} · {stats.Active} active"
                    : $"{baseTitle} · {stats.Total} member{(stats.Total == 1 ? "" : "s")}";
            }
            else
            {
                title = baseTitle;
            }

            rows.Add(new UserDashboardRow(
                Kind: kind,
                Id: r.RunId,
                Title: title,
                Status: r.Status,
                StartedAt: r.StartedAt,
                LastActivity: r.EndedAt ?? r.StartedAt,
                OwnerLabel: r.UserId,
                Preview: null,
                CostUsd: r.CostUsd,
                DetailRoute: isTeamRun
                    ? $"/orchestration?run={Uri.EscapeDataString(r.RunId)}"
                    : $"/command?run={Uri.EscapeDataString(r.RunId)}",
                WorkspaceId: r.WorkspaceId,
                ProjectId: r.ProjectId,
                IsOwn: isOwn,
                IsPrivate: r.IsPrivate));
        }

        // ── Sessions ───────────────────────────────────────────────────────
        var sessionRows = await SafeRecentSessionsAsync(userId, workspaceIds, ct).ConfigureAwait(false);
        ownSessions = sessionRows.Count(s => s.IsOwn);
        othersPublic += sessionRows.Count(s => !s.IsOwn);
        rows.AddRange(sessionRows);

        // ── Claws (unscoped today; see Phase 90 follow-up) ─────────────────
        var clawRows = await SafeListClawsAsync(ct).ConfigureAwait(false);
        foreach (var c in clawRows)
        {
            rows.Add(new UserDashboardRow(
                Kind: "claw",
                Id: c.Id,
                Title: c.Name,
                Status: c.Status,
                StartedAt: c.LastActivity,
                LastActivity: c.LastActivity,
                OwnerLabel: null,
                Preview: c.Preview,
                CostUsd: null,
                DetailRoute: c.DetailRoute,
                WorkspaceId: null,
                ProjectId: null,
                IsOwn: false,
                IsPrivate: false));
        }

        var userLabels = await ResolveUserLabelsAsync(rows, ct).ConfigureAwait(false);
        var resolved = rows
            .Select(r => r with { OwnerLabel = r.OwnerLabel is null ? null : userLabels.GetValueOrDefault(r.OwnerLabel, r.OwnerLabel) })
            .OrderByDescending(r => r.LastActivity)
            .Take(150)
            .ToList();

        // "Shared" = own items the user has made public (visible to teammates).
        // Counts from the trimmed grid so the stat matches what the user actually sees.
        var sharedByMe = resolved.Count(r => r.IsOwn && !r.IsPrivate && r.Kind != "claw");

        return new UserDashboardState(
            GeneratedAt: now,
            OwnMissions: ownMissions,
            OwnTeamRuns: ownTeamRuns,
            OwnAgentRuns: ownAgentRuns,
            OwnSessions: ownSessions,
            OthersPublicRows: sharedByMe,
            Claws: clawRows.Count,
            Rows: resolved);
    }

    /// <summary>
    /// Visibility rule for the User Dashboard. Returns true when:
    /// <list type="bullet">
    /// <item>the current user owns the record (regardless of privacy), OR</item>
    /// <item>the record is public AND belongs to a workspace the user is a member of.</item>
    /// </list>
    /// Other users' private records and records from non-member workspaces are excluded entirely.
    /// </summary>
    private static bool IsVisible(
        string? recordOwnerId,
        string? recordWorkspaceId,
        bool recordIsPrivate,
        string currentUserId,
        HashSet<string> userWorkspaceIds)
    {
        if (string.Equals(recordOwnerId, currentUserId, StringComparison.Ordinal))
            return true;
        if (recordIsPrivate) return false;
        if (string.IsNullOrEmpty(recordWorkspaceId)) return false;
        return userWorkspaceIds.Contains(recordWorkspaceId);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Dashboard aggregator must degrade gracefully if any one source fails — partial state is preferable to an exception propagating to the UI poll loop.")]
    private async Task<HashSet<string>> SafeListWorkspaceIdsAsync(string userId, CancellationToken ct)
    {
        try
        {
            var workspaces = await _workspaces.ListForUserAsync(userId, ct).ConfigureAwait(false);
            return new HashSet<string>(workspaces.Select(w => w.WorkspaceId), StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            LogSourceFailed(_logger, "workspaces", ex.Message);
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Dashboard aggregator must degrade gracefully if any one source fails — partial state is preferable to an exception propagating to the UI poll loop.")]
    private async Task<IReadOnlyList<Mission>> SafeListMissionsAsync(CancellationToken ct)
    {
        try { return await _missions.ListAsync(ownerUserId: null, status: null, limit: 200, ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            LogSourceFailed(_logger, "missions", ex.Message);
            return [];
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Dashboard aggregator must degrade gracefully if any one source fails — partial state is preferable to an exception propagating to the UI poll loop.")]
    private async Task<IReadOnlyList<AgentRunRecord>> SafeListRunsAsync(CancellationToken ct)
    {
        try { return await _runs.ListAsync(filter: null, limit: 200, ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            LogSourceFailed(_logger, "agent_runs", ex.Message);
            return [];
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Dashboard aggregator must degrade gracefully if any one source fails — partial state is preferable to an exception propagating to the UI poll loop.")]
    private async Task<IReadOnlyList<ClawConnectionInfo>> SafeListClawsAsync(CancellationToken ct)
    {
        try { return await _claws.ListAsync(ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            LogSourceFailed(_logger, "claws", ex.Message);
            return [];
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Dashboard aggregator must degrade gracefully if any one source fails — partial state is preferable to an exception propagating to the UI poll loop.")]
    private async Task<List<UserDashboardRow>> SafeRecentSessionsAsync(
        string userId,
        HashSet<string> workspaceIds,
        CancellationToken ct)
    {
        try
        {
            var summaries = await _sessions.ListWithTitlesAsync(ownerUserId: null, ct).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var rows = new List<UserDashboardRow>(Math.Min(summaries.Count, 150));

            foreach (var summary in summaries)
            {
                if (!IsVisible(summary.OwnerUserId, summary.WorkspaceId, summary.IsPrivate, userId, workspaceIds))
                    continue;

                var isOwn = string.Equals(summary.OwnerUserId, userId, StringComparison.Ordinal);
                // Own sessions: show full history. Others' public: 30-day window.
                if (!isOwn && summary.UpdatedAt < now.AddDays(-30)) continue;

                var title = !string.IsNullOrEmpty(summary.Title)
                    ? summary.Title
                    : Truncate(summary.SessionId, 16);
                var status = (now - summary.UpdatedAt) < TimeSpan.FromMinutes(2) ? "Active" : "Recent";

                rows.Add(new UserDashboardRow(
                    Kind: "session",
                    Id: summary.SessionId,
                    Title: title,
                    Status: status,
                    StartedAt: summary.UpdatedAt,
                    LastActivity: summary.UpdatedAt,
                    OwnerLabel: summary.OwnerUserId,
                    Preview: null,
                    CostUsd: null,
                    DetailRoute: $"/?session={Uri.EscapeDataString(summary.SessionId)}",
                    WorkspaceId: summary.WorkspaceId,
                    ProjectId: null,
                    IsOwn: isOwn,
                    IsPrivate: summary.IsPrivate));

                if (rows.Count >= 150) break;
            }
            return rows;
        }
        catch (Exception ex)
        {
            LogSourceFailed(_logger, "sessions", ex.Message);
            return [];
        }
    }

    private async Task<Dictionary<string, string>> ResolveUserLabelsAsync(
        IReadOnlyList<UserDashboardRow> rows, CancellationToken ct)
    {
        if (_users is null) return [];
        var ids = rows
            .Where(r => !string.IsNullOrEmpty(r.OwnerLabel))
            .Select(r => r.OwnerLabel!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            try
            {
                var user = await _users.GetAsync(id, ct).ConfigureAwait(false);
                if (user is not null)
                    result[id] = user.Email ?? user.Username;
            }
            catch (Exception ex) when (LogUserResolveFailed(_logger, id, ex)) { }
        }
        return result;
    }

    private static bool LogUserResolveFailed(ILogger logger, string userId, Exception ex)
    {
        LogSourceFailed(logger, $"user-resolve:{userId}", ex.Message);
        return true;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : string.Concat(s.AsSpan(0, max - 1), "…");
    }

    private static int TryCountPlanSteps(string? planJson)
    {
        if (string.IsNullOrWhiteSpace(planJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(planJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("steps", out var steps)
                && steps.ValueKind == JsonValueKind.Array)
            {
                return steps.GetArrayLength();
            }
        }
        catch (JsonException) { /* malformed — treat as 0 */ }
        return 0;
    }

    private readonly record struct ChildRunStats(int Total, int Active);

    private static readonly Action<ILogger, string, string, Exception?> _logSourceFailed =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(1, "UserDashboardSourceFailed"),
            "User Dashboard source {Source} failed: {Error}");
    private static void LogSourceFailed(ILogger logger, string source, string error) =>
        _logSourceFailed(logger, source, error, null);
}
