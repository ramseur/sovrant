namespace Sovrant.Tools.Skills;

/// <summary>
/// A parsed skill definition from a <c>.md</c> file with YAML frontmatter.
/// Skills are composable workflow packages invoked via <c>/trigger</c> slash commands
/// or programmatically by agents.
/// </summary>
public sealed record SkillDefinition(
    /// <summary>Kebab-case identifier (e.g., <c>tdd-workflow</c>).</summary>
    string Name,

    /// <summary>Human-readable description of the skill.</summary>
    string Description,

    /// <summary>Slash-command trigger (e.g., <c>/tdd</c>). May be empty if no trigger.</summary>
    string Trigger,

    /// <summary>
    /// Agent template names this skill may spawn (references Phase 21.5 templates).
    /// Empty means no agent delegation — the skill is a prompt overlay only.
    /// </summary>
    IReadOnlyList<string> Agents,

    /// <summary>
    /// Tools this skill is permitted to use. Empty means no restriction.
    /// </summary>
    IReadOnlyList<string> Tools,

    /// <summary>
    /// The full markdown body (after frontmatter) containing workflow steps,
    /// output format, and any inline script blocks.
    /// </summary>
    string Body);
