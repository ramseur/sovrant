namespace Sovrant.Runtime.Documents;

/// <summary>
/// Transforms a <see cref="DocumentRequest"/> into a document and writes
/// the output through the artifact store. One implementation per
/// <see cref="DocumentFormat"/>.
/// </summary>
public interface IDocumentGenerator
{
    /// <summary>The format this generator handles.</summary>
    DocumentFormat Format { get; }

    /// <summary>
    /// Generates the document and writes it to the artifact store using
    /// the scope supplied on the request. Returns a handle + relative path
    /// the caller can use to read the result back or surface a URL.
    /// </summary>
    Task<DocumentResult> GenerateAsync(DocumentRequest request, CancellationToken ct = default);
}
