using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Tools.Workflows;

/// <summary>
/// Phase 51 — exposes workflow lifecycle to running agents so they can
/// spawn sub-workflows, check status, or read the event journal from
/// inside a tool call. This lets a parent agent delegate a sub-goal
/// as a full workflow (with its own plan/execute/gate/journal cycle)
/// rather than trying to drive every step itself.
/// </summary>
public sealed class WorkflowTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Workflow", CreateSchema())
    {
        Description =
            "Manage autonomous workflows. Actions: 'create' (spawn a new workflow from a goal), " +
            "'run' (drive a workflow forward one engine cycle), 'get' (fetch current state), " +
            "'events' (read the full event journal), 'list' (list workflows, optionally filtered).",
    };

    private readonly IWorkflowStore _store;
    private readonly IWorkflowExecutor _executor;

    public WorkflowTool(IWorkflowStore store, IWorkflowExecutor executor)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var action = input.GetStringProp("action", "get");

        if (string.Equals(action, "create", StringComparison.OrdinalIgnoreCase))
            return await CreateAsync(input, ct).ConfigureAwait(false);
        if (string.Equals(action, "run", StringComparison.OrdinalIgnoreCase))
            return await RunAsync(input, ct).ConfigureAwait(false);
        if (string.Equals(action, "get", StringComparison.OrdinalIgnoreCase))
            return await GetAsync(input, ct).ConfigureAwait(false);
        if (string.Equals(action, "events", StringComparison.OrdinalIgnoreCase))
            return await EventsAsync(input, ct).ConfigureAwait(false);
        if (string.Equals(action, "list", StringComparison.OrdinalIgnoreCase))
            return await ListAsync(input, ct).ConfigureAwait(false);

        return $"Unknown action '{action}'. Valid actions: create, run, get, events, list.";
    }

    private async Task<string> CreateAsync(JsonElement input, CancellationToken ct)
    {
        var goal = input.GetStringProp("goal");
        if (string.IsNullOrWhiteSpace(goal))
            return "Error: 'goal' is required for action 'create'.";

        var workflow = await _store.CreateAsync(
            goal,
            sessionId: input.GetStringProp("session_id"),
            workspaceId: input.GetStringProp("workspace_id"),
            projectId: input.GetStringProp("project_id"),
            ownerUserId: input.GetStringProp("owner_user_id"),
            ct: ct).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            workflow_id = workflow.Id,
            status = workflow.Status switch
            {
                WorkflowStatus.Planning => "planning",
                WorkflowStatus.Running => "running",
                WorkflowStatus.AwaitingHuman => "awaiting_human",
                WorkflowStatus.Completed => "completed",
                WorkflowStatus.Failed => "failed",
                WorkflowStatus.Cancelled => "cancelled",
                _ => "unknown",
            },
            goal = workflow.Goal,
            created_at = workflow.CreatedAt,
        });
    }

    private async Task<string> RunAsync(JsonElement input, CancellationToken ct)
    {
        var workflowId = input.GetStringProp("workflow_id");
        if (string.IsNullOrWhiteSpace(workflowId))
            return "Error: 'workflow_id' is required for action 'run'.";

        try
        {
            var workflow = await _executor.RunAsync(workflowId, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                workflow_id = workflow.Id,
                status = workflow.Status switch
            {
                WorkflowStatus.Planning => "planning",
                WorkflowStatus.Running => "running",
                WorkflowStatus.AwaitingHuman => "awaiting_human",
                WorkflowStatus.Completed => "completed",
                WorkflowStatus.Failed => "failed",
                WorkflowStatus.Cancelled => "cancelled",
                _ => "unknown",
            },
                completed_at = workflow.CompletedAt,
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal))
        {
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> GetAsync(JsonElement input, CancellationToken ct)
    {
        var workflowId = input.GetStringProp("workflow_id");
        if (string.IsNullOrWhiteSpace(workflowId))
            return "Error: 'workflow_id' is required for action 'get'.";

        var workflow = await _store.GetAsync(workflowId, ct).ConfigureAwait(false);
        if (workflow is null)
            return $"Workflow '{workflowId}' not found.";

        return JsonSerializer.Serialize(new
        {
            workflow_id = workflow.Id,
            goal = workflow.Goal,
            status = workflow.Status switch
            {
                WorkflowStatus.Planning => "planning",
                WorkflowStatus.Running => "running",
                WorkflowStatus.AwaitingHuman => "awaiting_human",
                WorkflowStatus.Completed => "completed",
                WorkflowStatus.Failed => "failed",
                WorkflowStatus.Cancelled => "cancelled",
                _ => "unknown",
            },
            plan_json = workflow.PlanJson,
            created_at = workflow.CreatedAt,
            updated_at = workflow.UpdatedAt,
            completed_at = workflow.CompletedAt,
            workspace_id = workflow.WorkspaceId,
            project_id = workflow.ProjectId,
            owner_user_id = workflow.OwnerUserId,
        });
    }

    private async Task<string> EventsAsync(JsonElement input, CancellationToken ct)
    {
        var workflowId = input.GetStringProp("workflow_id");
        if (string.IsNullOrWhiteSpace(workflowId))
            return "Error: 'workflow_id' is required for action 'events'.";

        var events = await _store.GetEventsAsync(workflowId, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(events.Select(e => new
        {
            id = e.Id,
            event_type = e.EventType,
            payload = e.PayloadJson,
            timestamp = e.Timestamp,
        }).ToArray());
    }

    private async Task<string> ListAsync(JsonElement input, CancellationToken ct)
    {
        var owner = input.GetStringProp("owner_user_id");
        var statusText = input.GetStringProp("status");
        WorkflowStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(statusText)
            && Enum.TryParse<WorkflowStatus>(statusText, ignoreCase: true, out var parsed))
        {
            statusFilter = parsed;
        }

        var limit = input.GetIntProp("limit", 20);
        var workflows = await _store.ListAsync(owner, statusFilter, limit, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(workflows.Select(m => new
        {
            workflow_id = m.Id,
            goal = m.Goal,
            status = m.Status switch
            {
                WorkflowStatus.Planning => "planning",
                WorkflowStatus.Running => "running",
                WorkflowStatus.AwaitingHuman => "awaiting_human",
                WorkflowStatus.Completed => "completed",
                WorkflowStatus.Failed => "failed",
                WorkflowStatus.Cancelled => "cancelled",
                _ => "unknown",
            },
            created_at = m.CreatedAt,
        }).ToArray());
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "enum": ["create", "run", "get", "events", "list"],
                    "description": "The workflow action to perform."
                },
                "goal": {
                    "type": "string",
                    "description": "The workflow goal (required for 'create')."
                },
                "workflow_id": {
                    "type": "string",
                    "description": "Workflow ID (required for 'run', 'get', 'events')."
                },
                "owner_user_id": {
                    "type": "string",
                    "description": "Owner user ID for scoping (optional for 'create', 'list')."
                },
                "session_id": {
                    "type": "string",
                    "description": "Session ID to associate (optional for 'create')."
                },
                "workspace_id": {
                    "type": "string",
                    "description": "Workspace ID for scoping (optional for 'create')."
                },
                "project_id": {
                    "type": "string",
                    "description": "Project ID for scoping (optional for 'create')."
                },
                "status": {
                    "type": "string",
                    "description": "Filter by status (optional for 'list')."
                },
                "limit": {
                    "type": "integer",
                    "description": "Max results for 'list' (default 20)."
                }
            },
            "required": ["action"]
        }
        """).RootElement;
}
