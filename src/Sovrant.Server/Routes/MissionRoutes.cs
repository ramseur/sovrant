using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Missions;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 51 — HTTP surface for the mission layer. A mission is a
/// long-lived goal that the runtime pursues autonomously across one or
/// more engine runs, with an append-only event journal and an acceptance
/// gate. These endpoints let a CLI or UI create missions, inspect their
/// state, drive them forward one engine cycle at a time, and read the
/// canonical journal.
///
/// Endpoints:
/// <list type="bullet">
///   <item><c>POST /v1/missions</c> — create a mission in <c>planning</c> state</item>
///   <item><c>GET /v1/missions</c> — list missions (optionally filtered by owner/status)</item>
///   <item><c>GET /v1/missions/{id}</c> — fetch one mission record</item>
///   <item><c>POST /v1/missions/{id}/run</c> — drive the mission forward one engine cycle</item>
///   <item><c>GET /v1/missions/{id}/events</c> — full event journal for the mission</item>
/// </list>
/// </summary>
internal static class MissionRoutes
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/missions", async (
            CreateMissionRequest req,
            IMissionStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Goal))
                return Results.BadRequest(new { error = "goal is required." });

            var mission = await store.CreateAsync(
                req.Goal, req.SessionId, req.WorkspaceId, req.ProjectId, req.OwnerUserId, ct);
            return Results.Json(mission, s_jsonOptions, statusCode: 201);
        });

        app.MapGet("/v1/missions", async (
            string? ownerUserId,
            string? status,
            int? limit,
            IMissionStore store,
            CancellationToken ct) =>
        {
            MissionStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<MissionStatus>(status, ignoreCase: true, out var parsed))
                    return Results.BadRequest(new { error = $"unknown status '{status}'" });
                statusFilter = parsed;
            }

            var missions = await store.ListAsync(ownerUserId, statusFilter, limit ?? 100, ct);
            return Results.Json(new { missions }, s_jsonOptions);
        });

        app.MapGet("/v1/missions/{id}", async (
            string id,
            IMissionStore store,
            CancellationToken ct) =>
        {
            var mission = await store.GetAsync(id, ct);
            return mission is null
                ? Results.NotFound(new { error = $"mission '{id}' not found" })
                : Results.Json(mission, s_jsonOptions);
        });

        app.MapPost("/v1/missions/{id}/run", async (
            string id,
            IMissionExecutor executor,
            CancellationToken ct) =>
        {
            try
            {
                var mission = await executor.RunAsync(id, ct);
                return Results.Json(mission, s_jsonOptions);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal))
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapGet("/v1/missions/{id}/events", async (
            string id,
            IMissionStore store,
            CancellationToken ct) =>
        {
            var events = await store.GetEventsAsync(id, ct);
            return Results.Json(new { events }, s_jsonOptions);
        });

        app.MapGet("/v1/missions/{id}/export", async (
            string id,
            string? format,
            MissionExportService exporter,
            CancellationToken ct) =>
        {
            try
            {
                if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    var json = await exporter.ExportJsonAsync(id, ct);
                    return Results.Content(json, "application/json");
                }

                var md = await exporter.ExportMarkdownAsync(id, ct);
                return Results.Content(md, "text/markdown");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal))
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });
    }

    public sealed record CreateMissionRequest(
        string Goal,
        string? SessionId = null,
        string? WorkspaceId = null,
        string? ProjectId = null,
        string? OwnerUserId = null);
}
