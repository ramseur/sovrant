using Sovrant.Runtime.Metrics;

namespace Sovrant.Server.Routes;

/// <summary>Registers <c>GET /v1/cost</c>.</summary>
internal static class CostRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/cost", (
            CostDashboardService? dashboard,
            string? range) =>
        {
            if (dashboard is null)
                return Results.Ok(new { enabled = false, message = "Cost tracking is disabled (SOVRANT_COST_PROVIDER=none)" });

            var costRange = range?.ToLowerInvariant() switch
            {
                "daily" => CostRange.Daily,
                "weekly" => CostRange.Weekly,
                "monthly" => CostRange.Monthly,
                "all" => CostRange.All,
                _ => CostRange.Daily
            };

            var summary = dashboard.GetSummary(costRange);
            return Results.Ok(summary);
        });
    }
}
