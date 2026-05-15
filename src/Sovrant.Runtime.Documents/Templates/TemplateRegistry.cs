namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// Default <see cref="ITemplateRegistry"/> backed by DI-injected templates.
/// Duplicate IDs throw at construction so two templates can't silently
/// shadow each other.
/// </summary>
public sealed class TemplateRegistry : ITemplateRegistry
{
    private readonly Dictionary<string, IDocumentTemplate> _byId;

    public TemplateRegistry(IEnumerable<IDocumentTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        _byId = new Dictionary<string, IDocumentTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            if (!_byId.TryAdd(template.Id, template))
                throw new InvalidOperationException($"Duplicate template ID registered: '{template.Id}'");
        }
    }

    public IDocumentTemplate Resolve(string id)
    {
        if (_byId.TryGetValue(id, out var template))
            return template;
        throw new KeyNotFoundException($"No template registered with ID '{id}'.");
    }

    public bool TryResolve(string id, out IDocumentTemplate? template) =>
        _byId.TryGetValue(id, out template);

    public IReadOnlyCollection<IDocumentTemplate> All => _byId.Values;

    public IEnumerable<IDocumentTemplate> Find(string? industry = null, string? search = null)
    {
        foreach (var template in _byId.Values)
        {
            if (!string.IsNullOrWhiteSpace(industry) &&
                !string.Equals(template.Industry, industry, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var needle = search.Trim();
                if (template.Id.Contains(needle, StringComparison.OrdinalIgnoreCase)) { yield return template; continue; }
                if (template.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)) { yield return template; continue; }
                if (template.Description.Contains(needle, StringComparison.OrdinalIgnoreCase)) { yield return template; continue; }
                continue;
            }

            yield return template;
        }
    }
}
