using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Agents.Swarm;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers swarm endpoints:
/// <list type="bullet">
///   <item><c>POST /v1/swarm</c> — start a swarm (SSE stream)</item>
///   <item><c>GET /v1/swarm/{id}</c> — get swarm status</item>
///   <item><c>GET /v1/swarm/{id}/events</c> — replay JSONL events</item>
///   <item><c>GET /v1/swarm/sessions</c> — list all swarm sessions</item>
/// </list>
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
            SwarmOrchestrator orchestrator,
            SwarmQualityGate qualityGate,
            SwarmStateTracker stateTracker,
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

            // Execute with SSE streaming
            var result = await orchestrator.ExecuteAsync(plan, config, onEvent: evt =>
            {
                // Fire-and-forget SSE write (best effort for streaming)
                _ = WriteSseEventAsync(ctx.Response, evt.GetType().Name, evt, CancellationToken.None);
            }, ct);

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
        app.MapGet("/v1/swarm/{id}", (string id, SwarmStateTracker tracker) =>
        {
            var result = tracker.Get(id);
            return result is null
                ? Results.NotFound(new { error = $"No swarm found with ID '{id}'." })
                : Results.Ok(result);
        });

        // GET /v1/swarm/{id}/events — replay JSONL events
        app.MapGet("/v1/swarm/{id}/events", async (string id, SwarmSession session, CancellationToken ct) =>
        {
            if (!session.Exists(id))
                return Results.NotFound(new { error = $"No session found for swarm '{id}'." });

            var events = new List<object>();
            await foreach (var evt in session.ReplayAsync(id, ct))
            {
                if (evt is not null)
                    events.Add(evt);
            }
            return Results.Ok(events);
        });

        // GET /v1/swarm/sessions — list all sessions
        app.MapGet("/v1/swarm/sessions", (SwarmSession session) =>
        {
            return Results.Ok(session.ListSessions());
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
