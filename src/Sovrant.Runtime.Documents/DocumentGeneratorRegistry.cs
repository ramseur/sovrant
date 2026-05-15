namespace Sovrant.Runtime.Documents;

/// <summary>
/// Default <see cref="IDocumentGeneratorRegistry"/> backed by the set of
/// <see cref="IDocumentGenerator"/> instances supplied by DI. Later
/// registrations for the same format override earlier ones, which lets
/// tests swap in fakes cleanly.
/// </summary>
public sealed class DocumentGeneratorRegistry : IDocumentGeneratorRegistry
{
    private readonly Dictionary<DocumentFormat, IDocumentGenerator> _generators;

    public DocumentGeneratorRegistry(IEnumerable<IDocumentGenerator> generators)
    {
        ArgumentNullException.ThrowIfNull(generators);
        _generators = new Dictionary<DocumentFormat, IDocumentGenerator>();
        foreach (var generator in generators)
            _generators[generator.Format] = generator;
    }

    public IDocumentGenerator Resolve(DocumentFormat format)
    {
        if (_generators.TryGetValue(format, out var generator))
            return generator;

        throw new NotSupportedException(
            $"No document generator is registered for format '{format}'.");
    }

    public IReadOnlyCollection<DocumentFormat> SupportedFormats => _generators.Keys;
}
