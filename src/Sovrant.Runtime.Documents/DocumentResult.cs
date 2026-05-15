using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Documents;

/// <summary>
/// The outcome of a successful document generation. Callers can use
/// <see cref="Handle"/> and <see cref="RelativePath"/> to read the
/// document back via <see cref="IArtifactStore.ReadAsync"/>, or surface
/// <see cref="AccessUrl"/> directly to a UI.
/// </summary>
public sealed record DocumentResult
{
    /// <summary>The artifact handle for the run scope the document was written into.</summary>
    public required ArtifactHandle Handle { get; init; }

    /// <summary>The relative path inside the run scope.</summary>
    public required string RelativePath { get; init; }

    /// <summary>MIME content type of the output (e.g. <c>application/pdf</c>).</summary>
    public required string ContentType { get; init; }

    /// <summary>Size of the generated file in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// Local <c>file://</c> URL or presigned cloud URL — whatever the
    /// underlying artifact store returns. May be <see langword="null"/>
    /// if the store cannot vend a URL for this artifact.
    /// </summary>
    public Uri? AccessUrl { get; init; }
}
