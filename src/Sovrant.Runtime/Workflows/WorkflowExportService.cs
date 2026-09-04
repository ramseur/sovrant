using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 51 pm_export — generates human-readable Markdown or structured
/// JSON reports from a workflow's state and event journal. Designed to be
/// called from CLI commands (<c>sovrant workflow export</c>), API routes
/// (<c>GET /v1/missions/{id}/export</c>), or tools.
/// </summary>
public sealed class WorkflowExportService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    private readonly IWorkflowStore _store;

    public WorkflowExportService(IWorkflowStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Produces a Markdown report covering the workflow's goal, current
    /// status, plan snapshot, timeline (derived from the event journal),
    /// and summary statistics.
    /// </summary>
    public async Task<string> ExportMarkdownAsync(
        string workflowId, CancellationToken ct = default)
    {
        var workflow = await _store.GetAsync(workflowId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"workflow '{workflowId}' not found");

        var events = await _store.GetEventsAsync(workflowId, ct).ConfigureAwait(false);

        var sb = new StringBuilder();

        // ── Header ───────────────────────────────────────────────────────
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Workflow: {workflow.Goal}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**ID:** `{workflow.Id}`");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Status:** {workflow.Status}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Created:** {workflow.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        if (workflow.CompletedAt is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"**Completed:** {workflow.CompletedAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            var duration = workflow.CompletedAt.Value - workflow.CreatedAt;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"**Duration:** {FormatDuration(duration)}");
        }
        if (workflow.WorkspaceId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Workspace:** `{workflow.WorkspaceId}`");
        if (workflow.ProjectId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Project:** `{workflow.ProjectId}`");
        if (workflow.OwnerUserId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Owner:** `{workflow.OwnerUserId}`");
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
        var runCount = events.Count(e => e.EventType == WorkflowEventTypes.RunStarted);
        var replanCount = events.Count(e => e.EventType == WorkflowEventTypes.PlanRevised);
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Engine runs:** {runCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Plan revisions:** {replanCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Total events:** {events.Count}");

        return sb.ToString();
    }

    /// <summary>
    /// Produces a structured JSON export suitable for external tooling or
    /// archival. Contains the full workflow record plus the complete event
    /// journal.
    /// </summary>
    public async Task<string> ExportJsonAsync(
        string workflowId, CancellationToken ct = default)
    {
        var workflow = await _store.GetAsync(workflowId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"workflow '{workflowId}' not found");

        var events = await _store.GetEventsAsync(workflowId, ct).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            workflow = new
            {
                id = workflow.Id,
                goal = workflow.Goal,
                status = workflow.Status.ToString(),
                plan_json = workflow.PlanJson,
                created_at = workflow.CreatedAt,
                updated_at = workflow.UpdatedAt,
                completed_at = workflow.CompletedAt,
                workspace_id = workflow.WorkspaceId,
                project_id = workflow.ProjectId,
                owner_user_id = workflow.OwnerUserId,
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
                engine_runs = events.Count(e => e.EventType == WorkflowEventTypes.RunStarted),
                plan_revisions = events.Count(e => e.EventType == WorkflowEventTypes.PlanRevised),
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
                WorkflowEventTypes.WorkflowCreated =>
                    root.TryGetProperty("goal", out var g) ? Truncate(g.GetString(), 60) : "",
                WorkflowEventTypes.PlanRevised =>
                    root.TryGetProperty("plan_id", out var p) ? $"plan `{Truncate(p.GetString(), 20)}`" : "",
                WorkflowEventTypes.RunStarted =>
                    root.TryGetProperty("runtime_run_id", out var r) ? $"run `{Truncate(r.GetString(), 20)}`" : "",
                WorkflowEventTypes.RunCompleted =>
                    root.TryGetProperty("terminal_state", out var t) ? t.GetString() ?? "" : "",
                WorkflowEventTypes.Failed =>
                    root.TryGetProperty("error", out var e)
                        ? Truncate(e.GetString(), 60)
                        : root.TryGetProperty("reason", out var rr) ? Truncate(rr.GetString(), 60) : "",
                WorkflowEventTypes.AcceptanceApproved or WorkflowEventTypes.AcceptanceRejected =>
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
