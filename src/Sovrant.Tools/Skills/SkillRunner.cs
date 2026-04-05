using System.Globalization;
using System.Text;

namespace Sovrant.Tools.Skills;

/// <summary>
/// Executes a skill by constructing a prompt from the skill definition.
/// Returns the full skill prompt with metadata context, workflow steps,
/// and tool/agent constraints for the model to follow.
/// </summary>
public sealed class SkillRunner
{
    private readonly SkillRegistry _registry;

    public SkillRunner(SkillRegistry registry) => _registry = registry;

    /// <summary>
    /// Resolves and runs a skill by name or trigger.
    /// Returns the formatted skill prompt, or an error string if not found.
    /// </summary>
    public string Execute(string nameOrTrigger, string? arguments = null)
    {
        var skill = _registry.TryGetByName(nameOrTrigger)
                 ?? _registry.TryGetByTrigger(nameOrTrigger);

        if (skill is null)
            return $"Error: skill '{nameOrTrigger}' not found. Use /new-skill to create one.";

        return FormatPrompt(skill, arguments);
    }

    /// <summary>Lists all available skills with their triggers and descriptions.</summary>
    public string ListSkills()
    {
        var skills = _registry.All;
        if (skills.Count == 0)
            return "No skills available. Use /new-skill to create one.";

        var sb = new StringBuilder();
        sb.AppendLine("## Available Skills");
        sb.AppendLine();

        foreach (var skill in skills.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var trigger = string.IsNullOrWhiteSpace(skill.Trigger) ? "(no trigger)" : skill.Trigger;
            sb.AppendLine(CultureInfo.InvariantCulture, $"- **{skill.Name}** `{trigger}` — {skill.Description}");
        }

        return sb.ToString();
    }

    private static string FormatPrompt(SkillDefinition skill, string? arguments)
    {
        var sb = new StringBuilder();

        sb.AppendLine(CultureInfo.InvariantCulture, $"# Skill: {skill.Name}");
        if (!string.IsNullOrWhiteSpace(skill.Description))
            sb.AppendLine(CultureInfo.InvariantCulture, $"*{skill.Description}*");
        sb.AppendLine();

        if (skill.Tools.Count > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Allowed tools:** {string.Join(", ", skill.Tools)}");

        if (skill.Agents.Count > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Agent templates:** {string.Join(", ", skill.Agents)}");

        if (skill.Tools.Count > 0 || skill.Agents.Count > 0)
            sb.AppendLine();

        sb.AppendLine(skill.Body);

        var result = sb.ToString();
        if (!string.IsNullOrEmpty(arguments))
        {
            if (result.Contains("$ARGUMENTS", StringComparison.Ordinal))
                result = result.Replace("$ARGUMENTS", arguments, StringComparison.Ordinal);
            else
                result = $"{result}\n\n## Arguments\n{arguments}";
        }

        return result;
    }
}
