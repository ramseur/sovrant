using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Workflows;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 51 — HTTP surface for the workflow layer. A workflow is a
/// long-lived goal that the runtime pursues autonomously across one or
/// more engine runs, with an append-only event journal and an acceptance
/// gate. These endpoints let a CLI or UI create workflows, inspect their
/// state, drive them forward one engine cycle at a time, and read the
/// canonical journal.
///
/// Endpoints:
/// <list type="bullet">
///   <item><c>POST /v1/workflows</c> — create a workflow in <c>planning</c> state</item>
///   <item><c>GET /v1/workflows</c> — list workflows (optionally filtered by owner/status)</item>
///   <item><c>GET /v1/workflows/{id}</c> — fetch one workflow record</item>
///   <item><c>POST /v1/workflows/{id}/run</c> — drive the workflow forward one engine cycle</item>
///   <item><c>GET /v1/workflows/{id}/events</c> — full event journal for the workflow</item>
/// </list>
/// </summary>
internal static class WorkflowRoutes
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/workflows", async (
            CreateWorkflowRequest req,
            HttpContext ctx,
            IWorkflowStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Goal))
                return Results.BadRequest(new { error = "goal is required." });

            var callerId = HttpContextAuthExtensions.GetUserId(ctx);
            var workflow = await store.CreateAsync(
                req.Goal, req.SessionId, req.WorkspaceId, req.ProjectId, callerId, ct);
            return Results.Json(workflow, s_jsonOptions, statusCode: 201);
        });

        app.MapGet("/v1/workflows", async (
            string? ownerUserId,
            string? status,
            int? limit,
            HttpContext ctx,
            IWorkflowStore store,
            CancellationToken ct) =>
        {
            WorkflowStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<WorkflowStatus>(status, ignoreCase: true, out var parsed))
                    return Results.BadRequest(new { error = $"unknown status '{status}'" });
                statusFilter = parsed;
            }

            // Non-admin callers can only see their own workflows.
            if (!HttpContextAuthExtensions.IsAdmin(ctx))
                ownerUserId = HttpContextAuthExtensions.GetUserId(ctx);

            var workflows = await store.ListAsync(ownerUserId, statusFilter, limit ?? 100, ct);
            return Results.Json(new { workflows }, s_jsonOptions);
        });

        app.MapGet("/v1/workflows/{id}", async (
            string id,
            HttpContext ctx,
            IWorkflowStore store,
            CancellationToken ct) =>
        {
            var workflow = await store.GetAsync(id, ct);
            if (workflow is null)
                return Results.NotFound(new { error = $"workflow '{id}' not found" });
            if (!HttpContextAuthExtensions.IsAdmin(ctx) &&
                !string.Equals(workflow.OwnerUserId, HttpContextAuthExtensions.GetUserId(ctx), StringComparison.Ordinal))
                return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
            return Results.Json(workflow, s_jsonOptions);
        });

        app.MapPost("/v1/workflows/{id}/run", async (
            string id,
            HttpContext ctx,
            IWorkflowStore store,
            IWorkflowExecutor executor,
            CancellationToken ct) =>
        {
            var workflow = await store.GetAsync(id, ct);
            if (workflow is null)
                return Results.NotFound(new { error = $"workflow '{id}' not found" });
            if (!HttpContextAuthExtensions.IsAdmin(ctx) &&
                !string.Equals(workflow.OwnerUserId, HttpContextAuthExtensions.GetUserId(ctx), StringComparison.Ordinal))
                return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);

            try
            {
                var result = await executor.RunAsync(id, ct);
                return Results.Json(result, s_jsonOptions);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal))
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapGet("/v1/workflows/{id}/events", async (
            string id,
            HttpContext ctx,
            IWorkflowStore store,
            CancellationToken ct) =>
        {
            var workflow = await store.GetAsync(id, ct);
            if (workflow is null)
                return Results.NotFound(new { error = $"workflow '{id}' not found" });
            if (!HttpContextAuthExtensions.IsAdmin(ctx) &&
                !string.Equals(workflow.OwnerUserId, HttpContextAuthExtensions.GetUserId(ctx), StringComparison.Ordinal))
                return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
            var events = await store.GetEventsAsync(id, ct);
            return Results.Json(new { events }, s_jsonOptions);
        });

        app.MapGet("/v1/workflows/{id}/export", async (
            string id,
            string? format,
            HttpContext ctx,
            IWorkflowStore store,
            WorkflowExportService exporter,
            CancellationToken ct) =>
        {
            var workflow = await store.GetAsync(id, ct);
            if (workflow is null)
                return Results.NotFound(new { error = $"workflow '{id}' not found" });
            if (!HttpContextAuthExtensions.IsAdmin(ctx) &&
                !string.Equals(workflow.OwnerUserId, HttpContextAuthExtensions.GetUserId(ctx), StringComparison.Ordinal))
                return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);

            if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            {
                var json = await exporter.ExportJsonAsync(id, ct);
                return Results.Content(json, "application/json");
            }

            var md = await exporter.ExportMarkdownAsync(id, ct);
            return Results.Content(md, "text/markdown");
        });
    }

    public sealed record CreateWorkflowRequest(
        string Goal,
        string? SessionId = null,
        string? WorkspaceId = null,
        string? ProjectId = null,
        string? OwnerUserId = null);
}
