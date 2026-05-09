namespace Sovrant.Agents.Swarm;

/// <summary>
/// Persists and retrieves swarm orchestrator configuration from the
/// workspace settings store. Workspace ID <c>""</c> is the global default.
/// </summary>
public interface ISwarmConfigStore
{
    /// <summary>Returns the swarm config for the given workspace, falling back to global defaults.</summary>
    Task<SwarmConfig> GetAsync(string workspaceId = "", CancellationToken ct = default);

    /// <summary>Persists all swarm config fields for the given workspace.</summary>
    Task SetAsync(string workspaceId, SwarmConfig config, CancellationToken ct = default);
}
