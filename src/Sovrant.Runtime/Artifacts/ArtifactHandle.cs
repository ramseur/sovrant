namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// An opaque handle returned by <see cref="IArtifactStore.CreateRunScopeAsync"/>
/// that represents a materialized run-level artifact directory. Callers pass
/// this to <see cref="IArtifactStore.WriteAsync"/> and <see cref="IArtifactStore.ReadAsync"/>
/// without knowing the underlying path or key prefix.
/// </summary>
public sealed record ArtifactHandle
{
    /// <summary>The scope that was used to create this handle.</summary>
    public required ArtifactScope Scope { get; init; }

    /// <summary>
    /// The resolved root path (local) or key prefix (cloud) for this run.
    /// Use <see cref="Location"/> for display purposes.
    /// </summary>
    internal string ResolvedRoot { get; init; } = string.Empty;

    /// <summary>
    /// A human-readable location string (filesystem path or URI prefix)
    /// suitable for displaying to the user. Equivalent to
    /// <see cref="ResolvedRoot"/> but publicly accessible.
    /// </summary>
    public string Location => ResolvedRoot;
}
