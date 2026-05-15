namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// Looks up <see cref="IDocumentTemplate"/> instances by ID or industry.
/// </summary>
public interface ITemplateRegistry
{
    /// <summary>Returns the template or throws <see cref="KeyNotFoundException"/>.</summary>
    IDocumentTemplate Resolve(string id);

    /// <summary>Attempts to resolve — returns <see langword="false"/> if not found.</summary>
    bool TryResolve(string id, out IDocumentTemplate? template);

    /// <summary>All registered templates.</summary>
    IReadOnlyCollection<IDocumentTemplate> All { get; }

    /// <summary>
    /// Filters by industry and/or a free-text search over ID, name, and description.
    /// Both parameters are optional.
    /// </summary>
    IEnumerable<IDocumentTemplate> Find(string? industry = null, string? search = null);
}
