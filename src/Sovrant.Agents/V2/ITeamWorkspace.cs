namespace Sovrant.Agents.V2;

/// <summary>
/// [V2 Placeholder — not yet implemented. See Phase 19 roadmap.]
/// <para>
/// A named, isolated workspace shared by a team of agents. Provides per-team state,
/// artifact storage, and lifecycle boundaries. Multiple simultaneous teams may exist
/// in the same process, each with their own workspace.
/// </para>
/// </summary>
public interface ITeamWorkspace
{
    /// <summary>Gets the unique name of the team that owns this workspace.</summary>
    string TeamName { get; }

    /// <summary>Persists an artifact produced by a team member.</summary>
    Task StoreArtifactAsync(IArtifact artifact, CancellationToken ct = default);

    /// <summary>
    /// Retrieves an artifact by its identifier.
    /// Returns <see langword="null"/> if no artifact with that ID exists in this workspace.
    /// </summary>
    Task<IArtifact?> GetArtifactAsync(string artifactId, CancellationToken ct = default);

    /// <summary>Returns all artifacts currently stored in this workspace.</summary>
    Task<IReadOnlyList<IArtifact>> ListArtifactsAsync(CancellationToken ct = default);
}
