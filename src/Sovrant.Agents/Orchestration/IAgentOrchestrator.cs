namespace Sovrant.Agents.Orchestration;

/// <summary>
/// Phase 52 — unified entry point for all agentic execution. Replaces
/// the separate <c>SwarmOrchestrator</c> and <c>TeamDelegateTool</c>
/// execution paths with one engine that supports three creation modes
/// and opt-in feature flags for decomposition, file locking, quality
/// gates, and parallelism.
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Runs an ensemble of agents against a goal or pre-built plan.
    /// </summary>
    Task<EnsembleRunResult> RunAsync(EnsembleRunRequest request, CancellationToken ct = default);
}
