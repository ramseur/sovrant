using Sovrant.Agents.Templates;
using Sovrant.Runtime.Prompt;
using Sovrant.Tools.Skills;
using Sovrant.Tools.Templates;

namespace Sovrant.Tools;

/// <summary>
/// Bridges <see cref="SkillRegistry"/>, <see cref="AgentTemplateRegistry"/>, and
/// <see cref="UserToolTemplateRegistry"/> into the <see cref="ICapabilityCatalog"/>
/// abstraction consumed by <c>ConversationRuntime</c> for system prompt generation.
/// </summary>
internal sealed class CapabilityCatalog : ICapabilityCatalog
{
    public IReadOnlyList<(string Name, string Description, string? Trigger)> Skills { get; }
    public IReadOnlyList<string> AgentTemplateNames { get; }
    public IReadOnlyList<(string Slug, string Name, string Description, string Category, string Body)> ToolGuides { get; }

    public CapabilityCatalog(
        SkillRegistry skills,
        AgentTemplateRegistry templates,
        UserToolTemplateRegistry toolGuides)
    {
        Skills = skills.All
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => (s.Name, s.Description, string.IsNullOrWhiteSpace(s.Trigger) ? (string?)null : s.Trigger))
            .ToList();

        AgentTemplateNames = templates.All
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => t.Name)
            .ToList();

        ToolGuides = toolGuides.All
            .Where(g => !string.IsNullOrWhiteSpace(g.Body))
            .OrderBy(g => g.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Slug, g.Name, g.Description, g.Category, g.Body))
            .ToList();
    }
}
