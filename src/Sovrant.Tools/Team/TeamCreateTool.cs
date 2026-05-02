using System.Text.Json;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Teams;
using Sovrant.Agents.Templates;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Tools.Team;

/// <summary>Creates a new team member agent with a specified role and system prompt.</summary>
public sealed class TeamCreateTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TeamCreate", CreateSchema())
    {
        Description =
            "Creates a new team member agent. Specify a name, role, and system prompt. " +
            "Members are grouped under a team — pass the same 'team_name' on multiple " +
            "calls to add members to the same team (defaults to 'default'). " +
            "Optionally restrict the tools the agent can use. Returns the member ID.",
    };

    private readonly ITeamRegistry _registry;
    private readonly AgentTemplateRegistry? _templates;
    private readonly WorkspaceContext? _workspace;

    public TeamCreateTool(ITeamRegistry registry, AgentTemplateRegistry? templates = null)
        : this(registry, workspace: null, templates)
    {
    }

    public TeamCreateTool(ITeamRegistry registry, WorkspaceContext? workspace, AgentTemplateRegistry? templates = null)
    {
        _registry = registry;
        _templates = templates;
        _workspace = workspace;
    }

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var name = input.GetStringProp("name");
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult("Error: 'name' is required.");

        var teamName = GetStringOrNull(input, "team_name") ?? "default";
        var workspaceId = _workspace?.ArtifactScope?.WorkspaceId ?? WorkspaceIdentity.DefaultPersonal();
        var projectId = _workspace?.ArtifactScope?.ProjectId ?? ArtifactScope.DefaultProjectId;
        var teamId = EnsureTeam(workspaceId, projectId, teamName);

        // Optional template — provides defaults for prompt, tools, and model level.
        var templateName = GetStringOrNull(input, "template");
        var template = templateName is not null ? _templates?.TryGet(templateName) : null;
        if (templateName is not null && template is null)
            return Task.FromResult($"Error: unknown template '{templateName}'. " +
                "Use the 'templates' field on TeamStatus to list available templates.");

        // prompt is required unless a template provides it.
        var promptRaw = GetStringOrNull(input, "prompt");
        var prompt = promptRaw ?? template?.SystemPrompt;
        if (string.IsNullOrWhiteSpace(prompt))
            return Task.FromResult("Error: 'prompt' is required (or specify a 'template').");

        var roleStr = input.GetStringProp("role", template?.Role.ToString() ?? "general");
        if (!Enum.TryParse<AgentRole>(roleStr, ignoreCase: true, out var role))
            role = template?.Role ?? AgentRole.General;

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
        // Fall back to template's tool list if caller didn't specify one.
        allowedTools ??= template?.AllowedTools is { Count: > 0 } tTools ? tTools : null;

        var member = new TeamMemberInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            TeamId = teamId,
            WorkspaceId = workspaceId,
            ProjectId = projectId,
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
            team_id = teamId,
            name = member.Name,
            role = member.Role.ToString(),
            status = "created",
        });

        return Task.FromResult(result);
    }

    /// <summary>
    /// Finds an existing team with the given name in the workspace/project scope,
    /// or creates one. Returns the team ID. If the registry doesn't support teams
    /// (in-memory test registry), returns a synthetic ID — the member can still
    /// be registered without a persisted team.
    /// </summary>
    private string EnsureTeam(string workspaceId, string projectId, string teamName)
    {
        try
        {
            var existing = _registry.ListTeams(workspaceId)
                .FirstOrDefault(t =>
                    string.Equals(t.Name, teamName, StringComparison.Ordinal) &&
                    string.Equals(t.ProjectId ?? string.Empty, projectId, StringComparison.Ordinal));
            if (existing is not null)
                return existing.Id;

            var team = new TeamInfo(
                Id: $"team-{Guid.NewGuid():N}",
                WorkspaceId: workspaceId,
                ProjectId: projectId,
                Name: teamName,
                Description: teamName == "default" ? "Default team for this workspace/project." : null,
                Origin: "tool-created",
                CreatedBy: "system",
                CreatedAt: DateTimeOffset.UtcNow);
            _registry.CreateTeam(team);
            return team.Id;
        }
        catch (NotSupportedException)
        {
            return $"team-{Guid.NewGuid():N}";
        }
    }

    private static string? GetStringOrNull(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "name":          {"type": "string", "description": "Unique name for the team member."},
                "team_name":     {"type": "string", "description": "Team to add this member to. Reuse the same value across calls to build a multi-member team. Defaults to 'default'."},
                "template":      {"type": "string", "description": "Optional built-in template name (e.g. 'security-reviewer'). Provides default prompt and tools."},
                "role":          {"type": "string", "description": "Agent role: general, planner, coder, reviewer, executor, supervisor.", "default": "general"},
                "prompt":        {"type": "string", "description": "System prompt / instructions. Required unless a template is specified."},
                "allowed_tools": {"type": "array", "items": {"type": "string"}, "description": "Tools this agent may use. Defaults to the template's tool list if omitted."},
                "model":         {"type": "string", "description": "Optional model override for this agent."}
            },
            "required": ["name"]
        }
        """).RootElement;
}
