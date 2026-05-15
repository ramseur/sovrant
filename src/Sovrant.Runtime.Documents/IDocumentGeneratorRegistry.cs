namespace Sovrant.Runtime.Documents;

/// <summary>
/// Resolves an <see cref="IDocumentGenerator"/> by format. Lets callers
/// (tools, web handlers, CLI) stay decoupled from the specific generator
/// implementations — the DI layer wires up the concrete set.
/// </summary>
public interface IDocumentGeneratorRegistry
{
    /// <summary>
    /// Returns the generator registered for <paramref name="format"/>.
    /// Throws <see cref="NotSupportedException"/> if no generator is registered.
    /// </summary>
    IDocumentGenerator Resolve(DocumentFormat format);

    /// <summary>Enumerates the formats that have a registered generator.</summary>
    IReadOnlyCollection<DocumentFormat> SupportedFormats { get; }
}
