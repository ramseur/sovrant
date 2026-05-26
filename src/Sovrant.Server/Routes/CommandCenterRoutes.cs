using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.CommandCenter;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 90 / Phase 89 MVP — read-only Command Center surface.
/// Single endpoint that aggregates missions, agent_runs, and sessions
/// into a flat list of "what is the engine doing right now?" rows.
/// Both the Web cockpit and headless clients consume it.
/// </summary>
internal static class CommandCenterRoutes
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/command-center/state", async (
            CommandCenterAggregator aggregator,
            string? owner_user_id,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            // Non-admin callers can only see their own cockpit state.
            var viewerUserId = HttpContextAuthExtensions.GetUserId(ctx);
            if (!HttpContextAuthExtensions.IsAdmin(ctx))
                owner_user_id = viewerUserId;

            var state = await aggregator.GetActiveStateAsync(owner_user_id, viewerUserId, ct).ConfigureAwait(false);
            return Results.Json(state, s_jsonOptions);
        });
    }
}
