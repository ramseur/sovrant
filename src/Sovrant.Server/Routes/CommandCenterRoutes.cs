using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.CommandCenter;

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
            CancellationToken ct) =>
        {
            var state = await aggregator.GetActiveStateAsync(owner_user_id, ct).ConfigureAwait(false);
            return Results.Json(state, s_jsonOptions);
        });
    }
}
