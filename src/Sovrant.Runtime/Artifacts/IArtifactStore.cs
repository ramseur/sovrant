namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// Abstraction for storing agent-generated artifacts in a tenant-scoped layout.
/// <c>LocalArtifactStore</c> ships first; cloud-backed stores (S3/Azure/R2) slot
/// in later without touching callers.
/// </summary>
public interface IArtifactStore
{
    /// <summary>
    /// Materializes the scope tree and returns an opaque handle for the run.
    /// The handle is then passed to <see cref="WriteAsync"/> and <see cref="ReadAsync"/>.
    /// </summary>
    Task<ArtifactHandle> CreateRunScopeAsync(ArtifactScope scope, CancellationToken ct = default);

    /// <summary>
    /// Writes a single artifact file under the run scope.
    /// Rejects path-traversal attempts (e.g. <c>../../etc/passwd</c>).
    /// </summary>
    Task WriteAsync(ArtifactHandle handle, string relativePath, Stream content, string? contentType = null, CancellationToken ct = default);

    /// <summary>Reads an artifact file. Throws if not found.</summary>
    Task<Stream> ReadAsync(ArtifactHandle handle, string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Lists files at any scope level (run, project, workspace, user).
    /// Set <see cref="ArtifactScope.RunId"/> to <see langword="null"/> to list
    /// across all runs in a project.
    /// </summary>
    IAsyncEnumerable<ArtifactEntry> ListAsync(ArtifactScope scope, CancellationToken ct = default);

    /// <summary>
    /// Recursive delete at any scope level — powers cleanup when a
    /// run/project/workspace is deleted.
    /// </summary>
    Task DeleteAsync(ArtifactScope scope, CancellationToken ct = default);

    /// <summary>
    /// Returns a URI that can be used to access the artifact. Local store
    /// returns <c>file://</c> URIs; cloud stores return presigned URLs.
    /// Returns <see langword="null"/> if the artifact does not exist.
    /// </summary>
    Task<Uri?> GetAccessUrlAsync(ArtifactHandle handle, string relativePath, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Merges code-specific metadata into the run's <c>_manifest.json</c>.
    /// Called by scaffold tools after all files have been written.
    /// No-op in remote mode (the server writes the manifest during execution).
    /// </summary>
    Task SetCodeMetadataAsync(ArtifactHandle handle, CodeManifest metadata, CancellationToken ct = default);
}
