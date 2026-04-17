using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Documents;

/// <summary>
/// A request to generate a document. The body carries the source content
/// the generator will transform; <see cref="Scope"/> and <see cref="FileName"/>
/// determine where the output is written in the artifact store.
/// </summary>
public sealed record DocumentRequest
{
    /// <summary>The output format to produce.</summary>
    public required DocumentFormat Format { get; init; }

    /// <summary>
    /// The source content. For Markdown/StructuredPdf generators this is
    /// interpreted as Markdown; for Pdf it is treated as plain text.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>Optional document title used in headers/metadata.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// Relative file name (with extension) under the run scope. The
    /// generator will reject any path containing traversal segments.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>Artifact scope — workspace/project/run used to scope the output.</summary>
    public required ArtifactScope Scope { get; init; }
}
