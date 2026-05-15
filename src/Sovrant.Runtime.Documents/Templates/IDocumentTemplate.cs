using System.Text.Json;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// A named, industry-scoped document blueprint. Given typed data, a
/// template produces a <see cref="TemplateRenderResult"/> that the
/// caller pipes into the existing <see cref="IDocumentGenerator"/> so
/// the artifact write + URL path stays unchanged.
/// </summary>
/// <remarks>
/// Templates deliberately return a <see cref="TemplateRenderResult"/>
/// (format + body + default filename) rather than calling the generator
/// themselves. That keeps template logic pure/testable and lets callers
/// decide whether to override format/filename at render time.
/// </remarks>
public interface IDocumentTemplate
{
    /// <summary>Canonical ID (e.g. <c>business/invoice</c>).</summary>
    string Id { get; }

    /// <summary>Human-readable name for pickers and logs.</summary>
    string Name { get; }

    /// <summary>Industry classification (<c>business</c>, <c>legal</c>, <c>real-estate</c>, ...).</summary>
    string Industry { get; }

    /// <summary>Short description of what the template produces.</summary>
    string Description { get; }

    /// <summary>The format this template emits by default.</summary>
    DocumentFormat DefaultFormat { get; }

    /// <summary>Required + optional fields for render-time validation.</summary>
    IReadOnlyList<TemplateField> Fields { get; }

    /// <summary>
    /// Builds the body and chooses the default filename. The caller is
    /// responsible for handing the result off to an <see cref="IDocumentGenerator"/>.
    /// </summary>
    TemplateRenderResult Render(JsonElement data);
}

/// <summary>Body + filename produced by a template. Fed into the generator by the caller.</summary>
public sealed record TemplateRenderResult(DocumentFormat Format, string Body, string DefaultFileName, string? Title = null);
