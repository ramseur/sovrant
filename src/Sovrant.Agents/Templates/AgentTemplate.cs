using Sovrant.Agents.Models;

namespace Sovrant.Agents.Templates;

/// <summary>
/// A named, reusable agent configuration that bundles a system prompt, tool restrictions,
/// and a recommended capability level. Templates are identified by <see cref="Name"/> and
/// can be referenced by <c>TeamCreate</c> and <c>Agent</c> tools.
/// </summary>
public sealed record AgentTemplate(
    /// <summary>Kebab-case identifier (e.g., <c>security-reviewer</c>).</summary>
    string Name,

    /// <summary>Closest matching <see cref="AgentRole"/> for categorisation.</summary>
    AgentRole Role,

    /// <summary>
    /// The capability tier this template needs for best results.
    /// Resolved to an actual model via <c>SovrantConfig.ModelLevels</c>.
    /// </summary>
    RecommendedLevel RecommendedLevel,

    /// <summary>
    /// Tools this agent is permitted to use. An empty list means no restriction
    /// (all registered tools are available).
    /// </summary>
    IReadOnlyList<string> AllowedTools,

    /// <summary>The system prompt injected into the agent's runtime.</summary>
    string SystemPrompt,

    /// <summary>
    /// <see langword="true"/> when loaded from the install-dir built-in agents.
    /// Built-ins cannot be deleted; editing performs a silent copy-on-write to the
    /// user tier (<c>.sovrant/agents/</c>).
    /// </summary>
    bool IsBuiltIn = false,

    /// <summary>Optional one-line description shown in lists and help output.</summary>
    string? Description = null);
