using System.Text.Json;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Teams;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Team;

/// <summary>Removes a team member agent and cancels any in-flight tasks.</summary>
public sealed class TeamDeleteTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TeamDelete", CreateSchema())
    {
        Description = "Removes a team member by ID and cancels any running tasks.",
    };

    private readonly ITeamRegistry _registry;
    private readonly IMultiAgentSystem _agentSystem;

    public TeamDeleteTool(ITeamRegistry registry, IMultiAgentSystem agentSystem)
    {
        _registry = registry;
        _agentSystem = agentSystem;
    }

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var memberId = input.GetStringProp("member_id");
        if (string.IsNullOrWhiteSpace(memberId))
            return Task.FromResult("Error: 'member_id' is required.");

        var member = _registry.GetMember(memberId);
        if (member is null)
            return Task.FromResult($"Error: no team member found with ID '{memberId}'.");

        // Cancel any running task for this member
        _agentSystem.CancelTask(memberId);

        _registry.RemoveMember(memberId);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            member_id = memberId,
            name = member.Name,
            status = "deleted",
        }));
    }


    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "member_id": {"type": "string", "description": "The ID of the team member to remove."}
            },
            "required": ["member_id"]
        }
        """).RootElement;
}
