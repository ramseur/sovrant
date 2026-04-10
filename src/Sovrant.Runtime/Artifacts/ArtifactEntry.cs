namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// Represents a single artifact file returned by <see cref="IArtifactStore.ListAsync"/>.
/// </summary>
public sealed record ArtifactEntry
{
    /// <summary>Path relative to the scope root (e.g. "run-abc/report.md").</summary>
    public required string RelativePath { get; init; }

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Optional content type (MIME).</summary>
    public string? ContentType { get; init; }

    /// <summary>When the artifact was last modified.</summary>
    public DateTimeOffset LastModified { get; init; }

    /// <summary>The run ID this artifact belongs to, if known.</summary>
    public string? RunId { get; init; }
}
