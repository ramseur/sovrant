using System.Text.Json;
using Sovrant.Agents.Teams;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Team;

/// <summary>Returns the status of one or all team members.</summary>
public sealed class TeamStatusTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TeamStatus", CreateSchema())
    {
        Description =
            "Shows the status of team members. Pass a member_id for one member, " +
            "or omit it to see all members.",
    };

    private readonly ITeamRegistry _registry;

    public TeamStatusTool(ITeamRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var memberId = GetStringOrNull(input, "member_id");

        IReadOnlyList<TeamMemberInfo> members;
        if (memberId is not null)
        {
            var member = _registry.GetMember(memberId);
            if (member is null)
                return Task.FromResult($"Error: no team member found with ID '{memberId}'.");
            members = [member];
        }
        else
        {
            members = _registry.GetAllMembers();
        }

        var result = members.Select(m => new
        {
            id = m.Id,
            name = m.Name,
            role = m.Role.ToString(),
            status = m.Status.ToString(),
            created_at = m.CreatedAt,
            last_output = m.LastOutput is { Length: > 200 }
                ? m.LastOutput[..200] + "..."
                : m.LastOutput,
            last_error = m.LastError,
        });

        return Task.FromResult(JsonSerializer.Serialize(result));
    }

    private static string? GetStringOrNull(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "member_id": {"type": "string", "description": "Optional member ID. If omitted, returns all members."}
            }
        }
        """).RootElement;
}
