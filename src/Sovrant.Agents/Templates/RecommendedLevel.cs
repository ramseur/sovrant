namespace Sovrant.Agents.Templates;

/// <summary>
/// Indicates the capability tier a template recommends for best results.
/// Maps to admin-configured models via <c>SovrantConfig.ModelLevels</c>.
/// </summary>
public enum RecommendedLevel
{
    /// <summary>
    /// Deep reasoning, complex analysis, multi-step planning.
    /// Recommended for: architecture decisions, security audits, research synthesis.
    /// </summary>
    High,

    /// <summary>
    /// General-purpose coding, writing, and analysis tasks.
    /// Recommended for: implementation, documentation, data work.
    /// </summary>
    Standard,

    /// <summary>
    /// Quick triage, routing, or simple single-step tasks.
    /// Recommended for: classification, simple lookups, status checks.
    /// </summary>
    Fast,
}
