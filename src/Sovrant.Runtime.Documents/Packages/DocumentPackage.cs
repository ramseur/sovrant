namespace Sovrant.Runtime.Documents.Packages;

/// <summary>
/// A named bundle of templates that share an input data object — e.g.
/// a real-estate "closing package" of purchase agreement + closing
/// disclosure + property inspection rendered against the same
/// transaction data. The agent renders the bundle in one call instead
/// of orchestrating individual <c>DocumentFromTemplate</c> invocations.
/// </summary>
public sealed record DocumentPackage(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<DocumentPackageItem> Items);

/// <summary>One template inside a <see cref="DocumentPackage"/>.</summary>
/// <param name="TemplateId">Canonical template id (e.g. <c>real-estate/purchase-agreement</c>).</param>
/// <param name="Format">
/// Optional format override. <see langword="null"/> uses the template's
/// <see cref="Sovrant.Runtime.Documents.Templates.IDocumentTemplate.DefaultFormat"/>.
/// </param>
public sealed record DocumentPackageItem(string TemplateId, DocumentFormat? Format = null);

public interface IDocumentPackageRegistry
{
    bool TryResolve(string id, out DocumentPackage? package);
    IReadOnlyCollection<DocumentPackage> All { get; }
}
