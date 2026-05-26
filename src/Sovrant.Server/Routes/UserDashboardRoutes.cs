using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.UserDashboard;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 98 — read-only User Dashboard surface. Per-user cross-workspace
/// activity view. Always scoped to the authenticated caller — no admin
/// override; the admin-wide accountability view stays on Command Center.
/// </summary>
internal static class UserDashboardRoutes
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/user-dashboard/state", async (
            UserDashboardAggregator aggregator,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = HttpContextAuthExtensions.GetUserId(ctx);
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var state = await aggregator.GetStateAsync(userId, ct).ConfigureAwait(false);
            return Results.Json(state, s_jsonOptions);
        });
    }
}
