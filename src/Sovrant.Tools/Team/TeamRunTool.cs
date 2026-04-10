using System.Text.Json;
using Sovrant.Agents.Orchestration;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Team;

/// <summary>
/// Phase 52 — LLM-callable tool that runs an existing team against a
/// task with parallelism. Defaults to <c>decompose=false</c> (no
/// decomposition tax) since the LLM has already decided what to delegate.
/// </summary>
public sealed class TeamRunTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TeamRun", CreateSchema())
    {
        Description =
            "Runs an existing team against a task. Optionally enables parallel execution, " +
            "file locking, and quality gate. Returns the combined output from all agents.",
    };

    private readonly IAgentOrchestrator _orchestrator;
    private readonly ITeamRegistry _teamRegistry;

    public TeamRunTool(IAgentOrchestrator orchestrator, ITeamRegistry teamRegistry)
    {
        _orchestrator = orchestrator;
        _teamRegistry = teamRegistry;
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var teamId = input.GetStringProp("team_id");
        if (string.IsNullOrWhiteSpace(teamId))
            return "Error: 'team_id' is required.";

        var task = input.GetStringProp("task");
        if (string.IsNullOrWhiteSpace(task))
            return "Error: 'task' is required.";

        var team = _teamRegistry.GetTeam(teamId);
        if (team is null)
            return $"Error: team '{teamId}' not found.";

        var parallel = input.TryGetProperty("parallel", out var pEl) && pEl.GetBoolean();
        var lockFiles = input.TryGetProperty("lock_files", out var lfEl) && lfEl.GetBoolean();
        var qualityGate = input.TryGetProperty("quality_gate", out var qgEl) && qgEl.GetBoolean();

        var request = new EnsembleRunRequest
        {
            Goal = task,
            TeamId = teamId,
            WorkspaceId = team.WorkspaceId,
            ProjectId = team.ProjectId,
            UserId = team.CreatedBy,
            Decompose = false,
            LockFiles = lockFiles,
            QualityGate = qualityGate,
            MaxParallel = parallel ? null : 1,
        };

        var result = await _orchestrator.RunAsync(request, ct).ConfigureAwait(false);

        return JsonSerializer.Serialize(new
        {
            run_id = result.RunId,
            status = result.SwarmResult.Status.ToString(),
            output = result.SwarmResult.CombinedOutput,
            tokens_used = result.SwarmResult.TotalTokensUsed,
        });
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "team_id":      {"type": "string", "description": "ID of the team to run."},
                "task":         {"type": "string", "description": "The task to execute."},
                "parallel":     {"type": "boolean", "description": "Enable parallel execution (default: false)."},
                "lock_files":   {"type": "boolean", "description": "Enable file locking (default: false)."},
                "quality_gate": {"type": "boolean", "description": "Enable quality gate (default: false)."}
            },
            "required": ["team_id", "task"]
        }
        """).RootElement;
}
