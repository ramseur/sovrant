using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Swarm;
using Sovrant.Runtime.Storage;
using Sovrant.Server.Middleware;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers swarm endpoints:
/// <list type="bullet">
///   <item><c>POST /v1/swarm</c> — start a swarm (SSE stream)</item>
///   <item><c>GET /v1/swarm/{id}</c> — get swarm status</item>
///   <item><c>GET /v1/swarm/{id}/events</c> — replay events from <c>swarm_events</c></item>
///   <item><c>GET /v1/swarm/sessions</c> — list all swarm sessions, optionally filtered by workspace/project</item>
/// </list>
/// As of Phase 37.5 these read/write through <see cref="ISwarmEventStore"/>
/// instead of the legacy JSONL files under <c>~/.sovrant/swarm/sessions/</c>.
/// </summary>
internal static class SwarmRoutes
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Map(WebApplication app)
    {
        // POST /v1/swarm — start a swarm with SSE streaming
        app.MapPost("/v1/swarm", async (
            SwarmRunRequest request,
            SwarmConfig config,
            ISwarmDecomposer decomposer,
            ISwarmOrchestrator orchestrator,
            SwarmQualityGate qualityGate,
            ISwarmStateTracker stateTracker,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!config.Enabled)
                return Results.BadRequest(new { error = "Swarm orchestration is disabled. Enable in .sovrant/swarm.json." });

            if (string.IsNullOrWhiteSpace(request.Prompt))
                return Results.BadRequest(new { error = "prompt is required." });

            // Set SSE headers
            ctx.Response.ContentType = "text/event-stream; charset=utf-8";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            // Decompose
            SwarmPlan plan;
            try
            {
                plan = await decomposer.DecomposeAsync(request.Prompt, config, ct);
            }
            catch (InvalidOperationException ex)
            {
                await WriteSseEventAsync(ctx.Response, "error", new { error = ex.Message }, ct);
                await WriteSseDoneAsync(ctx.Response, ct);
                return Results.Empty;
            }

            if (!string.IsNullOrWhiteSpace(request.Team))
                plan.TeamId = request.Team;

            if (request.DryRun)
            {
                await WriteSseEventAsync(ctx.Response, "plan", plan, ct);
                await WriteSseDoneAsync(ctx.Response, ct);
                return Results.Empty;
            }

            // Build the per-run scoping context from the workspace middleware. Every
            // event written to swarm_events will be stamped with these IDs so the run
            // can later be filtered by workspace / project.
            var swarmContext = new SwarmExecutionContext(
                UserId: ctx.GetUserId(),
                WorkspaceId: ctx.GetWorkspaceId(),
                ProjectId: ctx.Request.Headers["X-Project-Id"].FirstOrDefault());

            // Execute with SSE streaming
            var logger = ctx.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Sovrant.Server.SwarmRoutes");
            var result = await orchestrator.ExecuteAsync(plan, config, onEvent: evt =>
            {
                // Phase I (9.4): SSE write is fire-and-forget because onEvent is sync.
                // Log failures (typically client disconnect) instead of silently swallowing.
                _ = WriteSseEventAsync(ctx.Response, evt.GetType().Name, evt, ct)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            logger?.LogDebug(t.Exception?.InnerException, "SSE write failed (client likely disconnected)");
                    }, TaskScheduler.Default);
            }, executionContext: swarmContext, ct: ct);

            // Quality gate
            if (config.QualityGateEnabled && result.Status == SwarmStatus.Completed)
            {
                result.Status = SwarmStatus.QualityReview;
                stateTracker.Update(result.SwarmId, result);

                var verdict = await qualityGate.ReviewAsync(
                    result.SwarmId, request.Prompt, result.CombinedOutput, ct);
                result.QualityGate = verdict;
                result.Status = SwarmStatus.Completed;
                stateTracker.Update(result.SwarmId, result);
            }

            await WriteSseEventAsync(ctx.Response, "result", result, ct);
            await WriteSseDoneAsync(ctx.Response, ct);
            return Results.Empty;
        });

        // GET /v1/swarm/{id} — get swarm status
        app.MapGet("/v1/swarm/{id}", (string id, ISwarmStateTracker tracker) =>
        {
            var result = tracker.Get(id);
            return result is null
                ? Results.NotFound(new { error = $"No swarm found with ID '{id}'." })
                : Results.Ok(result);
        });

        // GET /v1/swarm/{id}/events — replay events from swarm_events
        app.MapGet("/v1/swarm/{id}/events", async (string id, SwarmSession session, CancellationToken ct) =>
        {
            if (!await session.ExistsAsync(id, ct).ConfigureAwait(false))
                return Results.NotFound(new { error = $"No session found for swarm '{id}'." });

            var events = new List<object>();
            await foreach (var evt in session.ReplayAsync(id, ct))
            {
                if (evt is not null)
                    events.Add(evt);
            }
            return Results.Ok(events);
        });

        // GET /v1/swarm/sessions — list all sessions, optionally filtered by workspace/project.
        // Query params:  ?workspace_id=ws-...   ?project_id=proj-...   ?limit=50
        app.MapGet("/v1/swarm/sessions", async (HttpContext ctx, SwarmSession session, CancellationToken ct) =>
        {
            var workspaceId = ctx.Request.Query["workspace_id"].FirstOrDefault();
            var projectId = ctx.Request.Query["project_id"].FirstOrDefault();
            int? limit = int.TryParse(ctx.Request.Query["limit"].FirstOrDefault(), out var n) ? n : null;

            var filter = new SwarmListFilter(
                WorkspaceId: string.IsNullOrEmpty(workspaceId) ? null : workspaceId,
                ProjectId: string.IsNullOrEmpty(projectId) ? null : projectId,
                Limit: limit);

            return Results.Ok(await session.ListSessionsAsync(filter, ct).ConfigureAwait(false));
        });
    }

    private static async Task WriteSseEventAsync(HttpResponse response, string eventType, object data, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(data, s_jsonOptions);
        var line = $"event: {eventType}\ndata: {json}\n\n";
        await response.WriteAsync(line, Encoding.UTF8, ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task WriteSseDoneAsync(HttpResponse response, CancellationToken ct)
    {
        await response.WriteAsync("data: [DONE]\n\n", Encoding.UTF8, ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
}

internal sealed class SwarmRunRequest
{
    [JsonPropertyName("prompt")] public string Prompt { get; init; } = string.Empty;
    [JsonPropertyName("team")] public string? Team { get; init; }
    [JsonPropertyName("dry_run")] public bool DryRun { get; init; }
    [JsonPropertyName("budget")] public int? Budget { get; init; }
}
