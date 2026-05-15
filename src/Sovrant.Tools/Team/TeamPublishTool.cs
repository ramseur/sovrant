using System.Text.Json;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Team;

/// <summary>
/// Phase 52 — LLM-callable tool that publishes ephemeral swarm workers
/// as a named team so they can be reused in later conversations.
/// Marks the team's <c>origin = 'swarm-published'</c>.
/// </summary>
public sealed class TeamPublishTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TeamPublish", CreateSchema())
    {
        Description =
            "Publishes a set of agent workers as a named, reusable team. " +
            "Typically used after a swarm run to persist the ephemeral workers " +
            "so they can be called back in a later conversation.",
    };

    private readonly ITeamRegistry _teamRegistry;

    public TeamPublishTool(ITeamRegistry teamRegistry)
    {
        _teamRegistry = teamRegistry;
    }

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var name = input.GetStringProp("name");
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult("Error: 'name' is required.");

        var workspaceId = input.GetStringProp("workspace_id", "");
        var userId = input.GetStringProp("user_id", "system");
        var description = GetStringOrNull(input, "description");

        var teamId = $"team-{Guid.NewGuid():N}";
        var team = new TeamInfo(
            Id: teamId,
            WorkspaceId: workspaceId,
            ProjectId: GetStringOrNull(input, "project_id"),
            Name: name,
            Description: description,
            Origin: "swarm-published",
            CreatedBy: userId,
            CreatedAt: DateTimeOffset.UtcNow);

        _teamRegistry.CreateTeam(team);

        // Parse members array
        var memberCount = 0;
        if (input.TryGetProperty("members", out var membersEl) && membersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var memberEl in membersEl.EnumerateArray())
            {
                var memberName = memberEl.GetStringProp("name");
                if (string.IsNullOrWhiteSpace(memberName)) continue;

                var member = new TeamMemberInfo
                {
                    Id = $"member-{Guid.NewGuid():N}",
                    TeamId = teamId,
                    WorkspaceId = workspaceId,
                    Name = memberName,
                    Role = Sovrant.Agents.Models.AgentRole.General,
                    Template = GetStringOrNull(memberEl, "template"),
                    SystemPrompt = memberEl.GetStringProp("prompt", "You are a helpful agent."),
                    CreatedBy = userId,
                };
                _teamRegistry.RegisterMember(member);
                memberCount++;
            }
        }

        var result = JsonSerializer.Serialize(new
        {
            team_id = teamId,
            name,
            member_count = memberCount,
            origin = "swarm-published",
        });

        return Task.FromResult(result);
    }

    private static string? GetStringOrNull(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "name":         {"type": "string", "description": "Name for the published team."},
                "workspace_id": {"type": "string", "description": "Workspace scope."},
                "project_id":   {"type": "string", "description": "Optional project scope."},
                "user_id":      {"type": "string", "description": "User who created the team."},
                "description":  {"type": "string", "description": "Optional team description."},
                "members": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name":     {"type": "string"},
                            "template": {"type": "string"},
                            "prompt":   {"type": "string"}
                        },
                        "required": ["name"]
                    },
                    "description": "Members to add to the team."
                }
            },
            "required": ["name"]
        }
        """).RootElement;
}
