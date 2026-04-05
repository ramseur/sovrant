namespace Sovrant.Runtime.Governance;

/// <summary>No-op governance monitor for contexts where governance is disabled.</summary>
public sealed class NullGovernanceMonitor : IGovernanceMonitor
{
    /// <inheritdoc/>
    public Task<GovernanceVerdict> EvaluateAsync(GovernanceContext context, CancellationToken ct = default) =>
        Task.FromResult(GovernanceVerdict.Allowed);
}
