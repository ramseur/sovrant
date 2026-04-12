using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Skills;

/// <summary>
/// Creates a new skill definition file at <c>.sovrant/skills/{name}.md</c> in the
/// current project. Also registers it in the active <see cref="SkillRegistry"/>.
/// </summary>
public sealed class SkillCreateTool : ITool
{
    private readonly SkillRegistry _registry;

    private static readonly ToolDefinition s_definition = new("SkillCreate", CreateSchema())
    {
        Description =
            "Creates a new skill definition file in .sovrant/skills/{name}.md. " +
            "Provide the skill name, description, trigger, and workflow body. " +
            "The skill is immediately available for use.",
    };

    public SkillCreateTool(SkillRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var name = input.GetStringProp("name");
        if (string.IsNullOrWhiteSpace(name))
            return "Error: name is required.";

        // Sanitise: no path traversal
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

        // Build frontmatter
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine(CultureInfo.InvariantCulture, $"name: {name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"description: {description}");
        if (!string.IsNullOrWhiteSpace(trigger))
            sb.AppendLine(CultureInfo.InvariantCulture, $"trigger: {trigger}");
        if (agents.Length > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"agents: [{string.Join(", ", agents)}]");
        if (tools.Length > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"tools: [{string.Join(", ", tools)}]");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(body);

        var content = sb.ToString();

        // Write to project-local skills directory
        var skillsDir = Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "skills");
        Directory.CreateDirectory(skillsDir);
        var filePath = Path.Combine(skillsDir, string.Create(CultureInfo.InvariantCulture, $"{name}.md"));

        // Defense-in-depth: verify resolved path stays within skills directory
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(skillsDir), pathComparison))
            return "Error: name resolves outside the skills directory.";

        await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);

        // Register in active registry
        var skill = SkillParser.Parse(content);
        if (skill is not null)
            _registry.Register(skill);

        return $"Skill '{name}' created at {filePath}";
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
