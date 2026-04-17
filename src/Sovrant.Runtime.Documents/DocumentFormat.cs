namespace Sovrant.Runtime.Documents;

/// <summary>
/// Output formats supported by the document generation engine. Each format
/// maps to exactly one <see cref="IDocumentGenerator"/> implementation
/// registered in the <see cref="IDocumentGeneratorRegistry"/>.
/// </summary>
public enum DocumentFormat
{
    /// <summary>Plain Markdown (CommonMark).</summary>
    Markdown,

    /// <summary>Simple text-based PDF via PDFSharp.</summary>
    Pdf,

    /// <summary>Structured PDF (tables/headers/styles) via MigraDoc.</summary>
    StructuredPdf,
}
