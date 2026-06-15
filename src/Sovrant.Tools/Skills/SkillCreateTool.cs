using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Tools.Skills;

/// <summary>
/// Creates a new skill in the <see cref="IKnowledgeStore"/> at project scope (Phase 112).
/// The skill is immediately available via the refreshed <see cref="SkillRegistry"/>.
/// </summary>
public sealed class SkillCreateTool : ITool
{
    private readonly SkillRegistry _registry;
    private readonly IKnowledgeStore _store;

    private static readonly ToolDefinition s_definition = new("SkillCreate", CreateSchema())
    {
        Description =
            "Creates a new skill in the knowledge store. " +
            "Provide the skill name, description, trigger, and workflow body. " +
            "The skill is immediately available for use.",
    };

    public SkillCreateTool(SkillRegistry registry, IKnowledgeStore store)
    {
        _registry = registry;
        _store = store;
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var name = input.GetStringProp("name");
        if (string.IsNullOrWhiteSpace(name))
            return "Error: name is required.";

        if (name.Contains('/', StringComparison.Ordinal) ||
            name.Contains('\\', StringComparison.Ordinal) ||
            name.Contains("..", StringComparison.Ordinal) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Error: name must be a plain kebab-case identifier.";

        var description = input.GetStringProp("description", "Custom skill");
        var trigger = input.GetStringProp("trigger");
        var agents = input.GetStringArrayProp("agents");
        var tools = input.GetStringArrayProp("tools");
        var body = input.GetStringProp("body");

        if (string.IsNullOrWhiteSpace(body))
            return "Error: body is required (workflow steps and instructions).";

        var projectId = KnowledgeScope.ProjectIdFor(Directory.GetCurrentDirectory());
        var now = DateTimeOffset.UtcNow;
        var page = new KnowledgePage(
            KnowledgeId:   $"skill_{projectId}_{name}",
            Kind:          "skills",
            Slug:          name,
            Name:          name,
            Description:   description,
            Tier:          "User",
            Body:          body,
            WorkspaceId:   projectId,
            CreatedAt:     now,
            UpdatedAt:     now,
            Trigger:       string.IsNullOrWhiteSpace(trigger) ? null : trigger,
            Agents:        agents.Length > 0 ? JsonSerializer.Serialize(agents) : null,
            Tools:         tools.Length > 0 ? JsonSerializer.Serialize(tools) : null);

        await _store.UpsertAsync(page, ct).ConfigureAwait(false);
        _registry.Reload();

        return $"Skill '{name}' created in knowledge store (project scope).";
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "name":        {"type": "string", "description": "Kebab-case skill name (e.g. 'my-workflow')."},
                "description": {"type": "string", "description": "One-line description of the skill."},
                "trigger":     {"type": "string", "description": "Slash-command trigger (e.g. '/my-workflow')."},
                "agents":      {"type": "array", "items": {"type": "string"}, "description": "Agent template names to delegate to."},
                "tools":       {"type": "array", "items": {"type": "string"}, "description": "Tools this skill may use."},
                "body":        {"type": "string", "description": "Markdown workflow body with steps, output format, etc."}
            },
            "required": ["name", "body"]
        }
        """).RootElement;
}
