using System.Text.Json;
using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Team;

/// <summary>Creates a new team member agent with a specified role and system prompt.</summary>
public sealed class TeamCreateTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TeamCreate", CreateSchema())
    {
        Description =
            "Creates a new team member agent. Specify a name, role, and system prompt. " +
            "Optionally restrict the tools the agent can use. Returns the member ID.",
    };

    private readonly ITeamRegistry _registry;

    public TeamCreateTool(ITeamRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var name = input.GetStringProp("name");
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult("Error: 'name' is required.");

        var prompt = input.GetStringProp("prompt");
        if (string.IsNullOrWhiteSpace(prompt))
            return Task.FromResult("Error: 'prompt' is required.");

        var roleStr = input.GetStringProp("role", "general");
        if (!Enum.TryParse<AgentRole>(roleStr, ignoreCase: true, out var role))
            role = AgentRole.General;

        var model = GetStringOrNull(input, "model");

        IReadOnlyList<string>? allowedTools = null;
        if (input.TryGetProperty("allowed_tools", out var toolsEl) && toolsEl.ValueKind == JsonValueKind.Array)
        {
            allowedTools = toolsEl.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => s is not null)
                .Cast<string>()
                .ToList();
        }

        var member = new TeamMemberInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Role = role,
            SystemPrompt = prompt,
            AllowedTools = allowedTools,
            Model = model,
        };

        _registry.RegisterMember(member);

        var result = JsonSerializer.Serialize(new
        {
            member_id = member.Id,
            name = member.Name,
            role = member.Role.ToString(),
            status = "created",
        });

        return Task.FromResult(result);
    }


    private static string? GetStringOrNull(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "name":          {"type": "string", "description": "Unique name for the team member."},
                "role":          {"type": "string", "description": "Agent role: general, planner, coder, reviewer, executor, supervisor.", "default": "general"},
                "prompt":        {"type": "string", "description": "System prompt / instructions for this agent."},
                "allowed_tools": {"type": "array", "items": {"type": "string"}, "description": "Optional list of tool names this agent may use. If omitted, all tools are available."},
                "model":         {"type": "string", "description": "Optional model override for this agent."}
            },
            "required": ["name", "prompt"]
        }
        """).RootElement;
}
