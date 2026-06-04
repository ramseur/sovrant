using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Tools.Skills;

/// <summary>
/// Invokes a skill by name or trigger. Uses <see cref="SkillRegistry"/> for discovery
/// and <see cref="SkillRunner"/> for execution. Supports optional arguments that are
/// substituted for <c>$ARGUMENTS</c> in the skill body or appended.
/// </summary>
public sealed class SkillTool : ITool
{
    private readonly SkillRunner _runner;

    private static readonly ToolDefinition s_definition = new("Skill", CreateSchema())
    {
        Description =
            "Invokes a registered skill by name or trigger (e.g. 'tdd-workflow' or '/tdd'). " +
            "Returns the skill's workflow prompt with steps, tool constraints, and agent hints. " +
            "Use 'list' as the name to see all available skills.",
    };

    public SkillTool(SkillRunner runner) => _runner = runner;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var name = input.GetStringProp("name");
        if (string.IsNullOrWhiteSpace(name))
            return "Error: name is required.";

        if (name.Equals("list", StringComparison.OrdinalIgnoreCase))
            return _runner.ListSkills();

        var args = input.GetStringProp("args");
        var result = _runner.Execute(name, string.IsNullOrEmpty(args) ? null : args);

        // Phase 116 F — record attribution when the skill was found and its body returned.
        if (!result.StartsWith("Error:", StringComparison.Ordinal))
        {
            var scope = AttributionScope.Current;
            if (scope is not null)
                await scope.RecordAsync("skills", name, ct).ConfigureAwait(false);
        }

        return result;
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "Skill name or trigger (e.g. 'tdd-workflow' or '/tdd'). Use 'list' to see all available skills."},
                "args": {"type": "string", "description": "Optional arguments substituted for $ARGUMENTS in the skill template."}
            },
            "required": ["name"]
        }
        """).RootElement;
}
