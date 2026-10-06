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
///   <item><c>POST /v1/workflows/plan</c> — create a workflow and generate its plan for review (2.0)</item>
///   <item><c>PUT /v1/workflows/{id}/plan</c> — replace the plan with an edited one before it runs (2.0)</item>
///   <item><c>POST /v1/workflows/{id}/cancel</c> — cancel a workflow that hasn't finished (2.0)</item>
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
            WorkflowSessionNotifier sessionNotifier,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Goal))
                return Results.BadRequest(new { error = "goal is required." });

            var callerId = HttpContextAuthExtensions.GetUserId(ctx);
            var workflow = await store.CreateAsync(
                req.Goal, req.SessionId, req.WorkspaceId, req.ProjectId, callerId, ct);
            await sessionNotifier.NotifyCreatedAsync(workflow, ct);
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

        // 2.0 — plan first, then run: the same flow as the apps' "Generate Plan first".
        app.MapPost("/v1/workflows/plan", async (
            GeneratePlanRequest req,
            HttpContext ctx,
            WorkflowPlanningService planner,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Goal))
                return Results.BadRequest(new { error = "goal is required." });
            try
            {
                var workflow = await planner.GenerateAsync(
                    req.Goal, req.SessionId, req.WorkspaceId, req.ProjectId,
                    HttpContextAuthExtensions.GetUserId(ctx), ct);
                return Results.Json(workflow, s_jsonOptions, statusCode: 201);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 422, not 5xx: clients (the JS SDK included) retry 5xx automatically, and each
                // retry would create another workflow.
                return Results.Json(new { error = $"plan generation failed: {ex.Message}" }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        });

        app.MapPut("/v1/workflows/{id}/plan", async (
            string id,
            SavePlanRequest req,
            HttpContext ctx,
            IWorkflowStore store,
            WorkflowPlanningService planner,
            CancellationToken ct) =>
        {
            var workflow = await store.GetAsync(id, ct);
            if (workflow is null)
                return Results.NotFound(new { error = $"workflow '{id}' not found" });
            if (!HttpContextAuthExtensions.IsAdmin(ctx) &&
                !string.Equals(workflow.OwnerUserId, HttpContextAuthExtensions.GetUserId(ctx), StringComparison.Ordinal))
                return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);

            var steps = (req.Steps ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s.Intent))
                .Select((s, i) => new Sovrant.Runtime.Engine.RuntimeStep(
                    i, s.Intent!.Trim(),
                    string.IsNullOrWhiteSpace(s.ExpectedOutcome) ? "step completed successfully" : s.ExpectedOutcome.Trim(),
                    ParseTier(s.Tier)))
                .ToList();
            if (steps.Count == 0)
                return Results.BadRequest(new { error = "a plan needs at least one step with an intent." });

            try
            {
                var saved = await planner.SavePlanAsync(id, steps, ct);
                return Results.Json(saved, s_jsonOptions);
            }
            catch (InvalidOperationException ex)
            {
                // e.g. the workflow has already run: its plan is history, not a draft.
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
        });

        app.MapPost("/v1/workflows/{id}/cancel", async (
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

            if (!await store.CancelAsync(workflow, ct))
                return Results.Json(new { error = $"workflow '{id}' has already finished ({workflow.Status})." }, statusCode: StatusCodes.Status409Conflict);
            return Results.Json(await store.GetAsync(id, ct), s_jsonOptions);
        });
    }

    private static Sovrant.Runtime.Engine.RuntimeModelTier ParseTier(string? tier) => tier?.ToLowerInvariant() switch
    {
        "high" => Sovrant.Runtime.Engine.RuntimeModelTier.High,
        "fast" => Sovrant.Runtime.Engine.RuntimeModelTier.Fast,
        _ => Sovrant.Runtime.Engine.RuntimeModelTier.Standard,
    };

    // snake_case on the wire, like the other request types and the JS SDK. Without these the SDK's
    // session_id / workspace_id / project_id were silently ignored (the default binding is camelCase).
    public sealed record CreateWorkflowRequest(
        [property: JsonPropertyName("goal")] string Goal,
        [property: JsonPropertyName("session_id")] string? SessionId = null,
        [property: JsonPropertyName("workspace_id")] string? WorkspaceId = null,
        [property: JsonPropertyName("project_id")] string? ProjectId = null,
        [property: JsonPropertyName("owner_user_id")] string? OwnerUserId = null);

    public sealed record GeneratePlanRequest(
        [property: JsonPropertyName("goal")] string Goal,
        [property: JsonPropertyName("session_id")] string? SessionId = null,
        [property: JsonPropertyName("workspace_id")] string? WorkspaceId = null,
        [property: JsonPropertyName("project_id")] string? ProjectId = null);

    public sealed record SavePlanRequest(
        [property: JsonPropertyName("steps")] IReadOnlyList<PlanStepRequest>? Steps);

    /// <summary>One plan step: what to do, what "done" looks like, and the model tier (high, standard, fast).</summary>
    public sealed record PlanStepRequest(
        [property: JsonPropertyName("intent")] string? Intent,
        [property: JsonPropertyName("expected_outcome")] string? ExpectedOutcome = null,
        [property: JsonPropertyName("tier")] string? Tier = null);
}
