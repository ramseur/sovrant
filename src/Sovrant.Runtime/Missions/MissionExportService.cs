using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 51 pm_export — generates human-readable Markdown or structured
/// JSON reports from a mission's state and event journal. Designed to be
/// called from CLI commands (<c>sovrant mission export</c>), API routes
/// (<c>GET /v1/missions/{id}/export</c>), or tools.
/// </summary>
public sealed class MissionExportService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    private readonly IMissionStore _store;

    public MissionExportService(IMissionStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Produces a Markdown report covering the mission's goal, current
    /// status, plan snapshot, timeline (derived from the event journal),
    /// and summary statistics.
    /// </summary>
    public async Task<string> ExportMarkdownAsync(
        string missionId, CancellationToken ct = default)
    {
        var mission = await _store.GetAsync(missionId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"mission '{missionId}' not found");

        var events = await _store.GetEventsAsync(missionId, ct).ConfigureAwait(false);

        var sb = new StringBuilder();

        // ── Header ───────────────────────────────────────────────────────
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Mission: {mission.Goal}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**ID:** `{mission.Id}`");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Status:** {mission.Status}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Created:** {mission.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        if (mission.CompletedAt is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"**Completed:** {mission.CompletedAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            var duration = mission.CompletedAt.Value - mission.CreatedAt;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"**Duration:** {FormatDuration(duration)}");
        }
        if (mission.WorkspaceId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Workspace:** `{mission.WorkspaceId}`");
        if (mission.ProjectId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Project:** `{mission.ProjectId}`");
        if (mission.OwnerUserId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Owner:** `{mission.OwnerUserId}`");
        sb.AppendLine();

        // ── Timeline ─────────────────────────────────────────────────────
        sb.AppendLine("## Timeline");
        sb.AppendLine();
        if (events.Count == 0)
        {
            sb.AppendLine("_No events recorded._");
        }
        else
        {
            sb.AppendLine("| # | Timestamp | Event | Details |");
            sb.AppendLine("|---|-----------|-------|---------|");
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                var detail = SummarizePayload(e.EventType, e.PayloadJson);
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"| {i + 1} | {e.Timestamp:HH:mm:ss} | `{e.EventType}` | {detail} |");
            }
        }
        sb.AppendLine();

        // ── Stats ────────────────────────────────────────────────────────
        sb.AppendLine("## Summary");
        sb.AppendLine();
        var runCount = events.Count(e => e.EventType == MissionEventTypes.RunStarted);
        var replanCount = events.Count(e => e.EventType == MissionEventTypes.PlanRevised);
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Engine runs:** {runCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Plan revisions:** {replanCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Total events:** {events.Count}");

        return sb.ToString();
    }

    /// <summary>
    /// Produces a structured JSON export suitable for external tooling or
    /// archival. Contains the full mission record plus the complete event
    /// journal.
    /// </summary>
    public async Task<string> ExportJsonAsync(
        string missionId, CancellationToken ct = default)
    {
        var mission = await _store.GetAsync(missionId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"mission '{missionId}' not found");

        var events = await _store.GetEventsAsync(missionId, ct).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            mission = new
            {
                id = mission.Id,
                goal = mission.Goal,
                status = mission.Status.ToString(),
                plan_json = mission.PlanJson,
                created_at = mission.CreatedAt,
                updated_at = mission.UpdatedAt,
                completed_at = mission.CompletedAt,
                workspace_id = mission.WorkspaceId,
                project_id = mission.ProjectId,
                owner_user_id = mission.OwnerUserId,
            },
            events = events.Select(e => new
            {
                id = e.Id,
                event_type = e.EventType,
                payload = e.PayloadJson,
                timestamp = e.Timestamp,
            }).ToArray(),
            stats = new
            {
                total_events = events.Count,
                engine_runs = events.Count(e => e.EventType == MissionEventTypes.RunStarted),
                plan_revisions = events.Count(e => e.EventType == MissionEventTypes.PlanRevised),
            },
        }, s_jsonOptions);
    }

    private static string SummarizePayload(string eventType, string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            return eventType switch
            {
                MissionEventTypes.MissionCreated =>
                    root.TryGetProperty("goal", out var g) ? Truncate(g.GetString(), 60) : "",
                MissionEventTypes.PlanRevised =>
                    root.TryGetProperty("plan_id", out var p) ? $"plan `{Truncate(p.GetString(), 20)}`" : "",
                MissionEventTypes.RunStarted =>
                    root.TryGetProperty("runtime_run_id", out var r) ? $"run `{Truncate(r.GetString(), 20)}`" : "",
                MissionEventTypes.RunCompleted =>
                    root.TryGetProperty("terminal_state", out var t) ? t.GetString() ?? "" : "",
                MissionEventTypes.Failed =>
                    root.TryGetProperty("error", out var e)
                        ? Truncate(e.GetString(), 60)
                        : root.TryGetProperty("reason", out var rr) ? Truncate(rr.GetString(), 60) : "",
                MissionEventTypes.AcceptanceApproved or MissionEventTypes.AcceptanceRejected =>
                    root.TryGetProperty("reason", out var ar) ? Truncate(ar.GetString(), 60) : "",
                _ => "",
            };
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return string.Create(CultureInfo.InvariantCulture, $"{ts.TotalDays:F1} days");
        if (ts.TotalHours >= 1)
            return string.Create(CultureInfo.InvariantCulture, $"{ts.TotalHours:F1} hours");
        if (ts.TotalMinutes >= 1)
            return string.Create(CultureInfo.InvariantCulture, $"{ts.TotalMinutes:F0} min");
        return string.Create(CultureInfo.InvariantCulture, $"{ts.TotalSeconds:F0}s");
    }
}
