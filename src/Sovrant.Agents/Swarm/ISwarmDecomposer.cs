namespace Sovrant.Agents.Swarm;

/// <summary>
/// Decomposes a user prompt into a <see cref="SwarmPlan"/> — a task DAG with
/// dependency-based wave assignments.
/// </summary>
public interface ISwarmDecomposer
{
    /// <summary>
    /// Decomposes <paramref name="prompt"/> into a parallel task plan.
    /// </summary>
    Task<SwarmPlan> DecomposeAsync(string prompt, SwarmConfig config, CancellationToken ct = default);
}
