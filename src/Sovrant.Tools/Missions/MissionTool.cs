using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Missions;

namespace Sovrant.Tools.Missions;

/// <summary>
/// Phase 51 — exposes mission lifecycle to running agents so they can
/// spawn sub-missions, check status, or read the event journal from
/// inside a tool call. This lets a parent agent delegate a sub-goal
/// as a full mission (with its own plan/execute/gate/journal cycle)
/// rather than trying to drive every step itself.
/// </summary>
public sealed class MissionTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Mission", CreateSchema())
    {
        Description =
            "Manage autonomous missions. Actions: 'create' (spawn a new mission from a goal), " +
            "'run' (drive a mission forward one engine cycle), 'get' (fetch current state), " +
            "'events' (read the full event journal), 'list' (list missions, optionally filtered).",
    };

    private readonly IMissionStore _store;
    private readonly IMissionExecutor _executor;

    public MissionTool(IMissionStore store, IMissionExecutor executor)
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

        var mission = await _store.CreateAsync(
            goal,
            sessionId: input.GetStringProp("session_id"),
            workspaceId: input.GetStringProp("workspace_id"),
            projectId: input.GetStringProp("project_id"),
            ownerUserId: input.GetStringProp("owner_user_id"),
            ct: ct).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            mission_id = mission.Id,
            status = mission.Status switch
            {
                MissionStatus.Planning => "planning",
                MissionStatus.Running => "running",
                MissionStatus.AwaitingHuman => "awaiting_human",
                MissionStatus.Completed => "completed",
                MissionStatus.Failed => "failed",
                MissionStatus.Cancelled => "cancelled",
                _ => "unknown",
            },
            goal = mission.Goal,
            created_at = mission.CreatedAt,
        });
    }

    private async Task<string> RunAsync(JsonElement input, CancellationToken ct)
    {
        var missionId = input.GetStringProp("mission_id");
        if (string.IsNullOrWhiteSpace(missionId))
            return "Error: 'mission_id' is required for action 'run'.";

        try
        {
            var mission = await _executor.RunAsync(missionId, ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                mission_id = mission.Id,
                status = mission.Status switch
            {
                MissionStatus.Planning => "planning",
                MissionStatus.Running => "running",
                MissionStatus.AwaitingHuman => "awaiting_human",
                MissionStatus.Completed => "completed",
                MissionStatus.Failed => "failed",
                MissionStatus.Cancelled => "cancelled",
                _ => "unknown",
            },
                completed_at = mission.CompletedAt,
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal))
        {
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> GetAsync(JsonElement input, CancellationToken ct)
    {
        var missionId = input.GetStringProp("mission_id");
        if (string.IsNullOrWhiteSpace(missionId))
            return "Error: 'mission_id' is required for action 'get'.";

        var mission = await _store.GetAsync(missionId, ct).ConfigureAwait(false);
        if (mission is null)
            return $"Mission '{missionId}' not found.";

        return JsonSerializer.Serialize(new
        {
            mission_id = mission.Id,
            goal = mission.Goal,
            status = mission.Status switch
            {
                MissionStatus.Planning => "planning",
                MissionStatus.Running => "running",
                MissionStatus.AwaitingHuman => "awaiting_human",
                MissionStatus.Completed => "completed",
                MissionStatus.Failed => "failed",
                MissionStatus.Cancelled => "cancelled",
                _ => "unknown",
            },
            plan_json = mission.PlanJson,
            created_at = mission.CreatedAt,
            updated_at = mission.UpdatedAt,
            completed_at = mission.CompletedAt,
            workspace_id = mission.WorkspaceId,
            project_id = mission.ProjectId,
            owner_user_id = mission.OwnerUserId,
        });
    }

    private async Task<string> EventsAsync(JsonElement input, CancellationToken ct)
    {
        var missionId = input.GetStringProp("mission_id");
        if (string.IsNullOrWhiteSpace(missionId))
            return "Error: 'mission_id' is required for action 'events'.";

        var events = await _store.GetEventsAsync(missionId, ct).ConfigureAwait(false);
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
        MissionStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(statusText)
            && Enum.TryParse<MissionStatus>(statusText, ignoreCase: true, out var parsed))
        {
            statusFilter = parsed;
        }

        var limit = input.GetIntProp("limit", 20);
        var missions = await _store.ListAsync(owner, statusFilter, limit, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(missions.Select(m => new
        {
            mission_id = m.Id,
            goal = m.Goal,
            status = m.Status switch
            {
                MissionStatus.Planning => "planning",
                MissionStatus.Running => "running",
                MissionStatus.AwaitingHuman => "awaiting_human",
                MissionStatus.Completed => "completed",
                MissionStatus.Failed => "failed",
                MissionStatus.Cancelled => "cancelled",
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
                    "description": "The mission action to perform."
                },
                "goal": {
                    "type": "string",
                    "description": "The mission goal (required for 'create')."
                },
                "mission_id": {
                    "type": "string",
                    "description": "Mission ID (required for 'run', 'get', 'events')."
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
