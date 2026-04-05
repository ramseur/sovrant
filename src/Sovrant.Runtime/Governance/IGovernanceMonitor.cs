namespace Sovrant.Runtime.Governance;

/// <summary>
/// Evaluates tool operations against governance rules (secret detection,
/// dangerous command blocking, config protection) and returns a verdict.
/// </summary>
public interface IGovernanceMonitor
{
    /// <summary>Evaluates the given context against all governance rules.</summary>
    /// <returns>A verdict indicating whether the operation is allowed, warned, or blocked.</returns>
    Task<GovernanceVerdict> EvaluateAsync(GovernanceContext context, CancellationToken ct = default);
}
