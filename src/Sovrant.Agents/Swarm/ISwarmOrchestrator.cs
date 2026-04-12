namespace Sovrant.Agents.Swarm;

/// <summary>Executes a <see cref="SwarmPlan"/> wave-by-wave with concurrency control and event emission.</summary>
public interface ISwarmOrchestrator
{
    /// <summary>Executes a swarm plan end-to-end. Emits events via <paramref name="onEvent"/> callback.</summary>
    Task<SwarmResult> ExecuteAsync(
        SwarmPlan plan,
        SwarmConfig config,
        Action<SwarmEvent>? onEvent = null,
        SwarmExecutionContext? executionContext = null,
        CancellationToken ct = default);
}
