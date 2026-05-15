using Sovrant.Agents.Templates;
using Sovrant.Runtime.Prompt;
using Sovrant.Tools.Skills;

namespace Sovrant.Tools;

/// <summary>
/// Bridges <see cref="SkillRegistry"/> and <see cref="AgentTemplateRegistry"/>
/// into the <see cref="ICapabilityCatalog"/> abstraction consumed by
/// <c>ConversationRuntime</c> for system prompt generation.
/// </summary>
internal sealed class CapabilityCatalog : ICapabilityCatalog
{
    public IReadOnlyList<(string Name, string Description, string? Trigger)> Skills { get; }
    public IReadOnlyList<string> AgentTemplateNames { get; }

    public CapabilityCatalog(SkillRegistry skills, AgentTemplateRegistry templates)
    {
        Skills = skills.All
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => (s.Name, s.Description, string.IsNullOrWhiteSpace(s.Trigger) ? (string?)null : s.Trigger))
            .ToList();

        AgentTemplateNames = templates.All
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => t.Name)
            .ToList();
    }
}
