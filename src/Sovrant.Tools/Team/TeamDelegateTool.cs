using System.Collections.Concurrent;
using System.Text.Json;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Team;

/// <summary>
/// Delegates a task to a team member agent. Creates the agent on first delegation
/// (lazy initialization), dispatches the task through <see cref="IOrchestrationSystem"/>,
/// and returns the result.
/// </summary>
public sealed class TeamDelegateTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TeamDelegate", CreateSchema())
    {
        Description =
            "Delegates a task to a team member. The member must have been created with TeamCreate first. " +
            "Pass the member_id and a prompt describing the task. Returns the agent's output.",
    };

    private readonly ITeamRegistry _registry;
    private readonly IOrchestrationSystem _agentSystem;
    private readonly Func<TeamMemberInfo, IAgent> _agentFactory;
    private readonly ConcurrentDictionary<string, byte> _initialized = new(StringComparer.Ordinal);

    public TeamDelegateTool(
        ITeamRegistry registry,
        IOrchestrationSystem agentSystem,
        Func<TeamMemberInfo, IAgent> agentFactory)
    {
        _registry = registry;
        _agentSystem = agentSystem;
        _agentFactory = agentFactory;
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var memberId = input.GetStringProp("member_id");
        if (string.IsNullOrWhiteSpace(memberId))
            return "Error: 'member_id' is required.";

        var prompt = input.GetStringProp("prompt");
        if (string.IsNullOrWhiteSpace(prompt))
            return "Error: 'prompt' is required.";

        var member = _registry.GetMember(memberId);
        if (member is null)
            return $"Error: no team member found with ID '{memberId}'.";

        // Lazy-create and register the agent on first delegation
        if (_initialized.TryAdd(member.Id, 0))
        {
            var agent = _agentFactory(member);
            _agentSystem.RegisterAgent(agent);
        }

        member.Status = TeamMemberStatus.Running;

        var task = AgentTask.Create(prompt, member.Name);
        var result = await _agentSystem.RunTaskAsync(task, ct).ConfigureAwait(false);

        if (result.Success)
        {
            member.Status = TeamMemberStatus.Completed;
            member.LastOutput = result.Output;
            member.LastError = null;
        }
        else
        {
            member.Status = TeamMemberStatus.Failed;
            member.LastError = result.Error;
            member.LastOutput = null;
        }

        return result.Success
            ? result.Output
            : $"Agent error: {result.Error}";
    }


    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "member_id": {"type": "string", "description": "The ID of the team member to delegate to."},
                "prompt":    {"type": "string", "description": "The task prompt to send to the agent."}
            },
            "required": ["member_id", "prompt"]
        }
        """).RootElement;
}
