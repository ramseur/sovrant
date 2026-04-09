using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Storage;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 51 — read-only introspection for the engine's planner/executor
/// layer. These endpoints let the CLI, UI, and mission layer inspect
/// what the engine has been doing without needing direct DB access.
///
/// Endpoints:
/// <list type="bullet">
///   <item><c>GET /v1/engine/runs/{runtimeRunId}/trace</c> — full ordered trace for one run</item>
///   <item><c>GET /v1/engine/runs/in-flight</c> — list run ids that crashed mid-step and have not been recovered</item>
///   <item><c>POST /v1/engine/runs/recover</c> — ask EngineRecovery to close out in-flight runs and return the list it touched</item>
///   <item><c>DELETE /v1/engine/runs/{runtimeRunId}</c> — drop every trace row for a run (used by test tooling and per-run cleanup)</item>
/// </list>
/// </summary>
internal static class EngineRoutes
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void Map(WebApplication app)
    {
        // GET /v1/engine/runs/{runtimeRunId}/trace — load a run's full trace.
        app.MapGet("/v1/engine/runs/{runtimeRunId}/trace", async (
            string runtimeRunId,
            IRuntimeTraceStore traceStore,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(runtimeRunId))
                return Results.BadRequest(new { error = "runtimeRunId is required." });

            var entries = await traceStore.LoadAsync(runtimeRunId, ct);
            if (entries.Count == 0)
                return Results.NotFound(new { error = $"No trace found for run '{runtimeRunId}'." });

            return Results.Json(new TraceResponse(runtimeRunId, entries), s_jsonOptions);
        });

        // GET /v1/engine/runs/in-flight — list runs with orphaned step_started rows.
        app.MapGet("/v1/engine/runs/in-flight", async (
            IRuntimeTraceStore traceStore,
            CancellationToken ct) =>
        {
            var ids = await traceStore.ListInFlightRunsAsync(ct);
            return Results.Json(new { runtime_run_ids = ids }, s_jsonOptions);
        });

        // POST /v1/engine/runs/recover — run EngineRecovery and return what it closed out.
        app.MapPost("/v1/engine/runs/recover", async (
            IEngineRecovery recovery,
            CancellationToken ct) =>
        {
            var recovered = await recovery.RecoverAsync(ct);
            return Results.Json(new { recovered }, s_jsonOptions);
        });

        // DELETE /v1/engine/runs/{runtimeRunId} — drop trace rows for a run.
        app.MapDelete("/v1/engine/runs/{runtimeRunId}", async (
            string runtimeRunId,
            IRuntimeTraceStore traceStore,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(runtimeRunId))
                return Results.BadRequest(new { error = "runtimeRunId is required." });

            var deleted = await traceStore.DeleteRunAsync(runtimeRunId, ct);
            return Results.Ok(new { runtime_run_id = runtimeRunId, deleted_rows = deleted });
        });
    }

    private sealed record TraceResponse(
        string RuntimeRunId,
        IReadOnlyList<RuntimeTraceEntry> Entries);
}
