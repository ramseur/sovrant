namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// A user-authored markdown document template loaded from disk. Sits alongside
/// the code-defined <see cref="IDocumentTemplate"/> registry. Frontmatter holds
/// metadata; the body is markdown that the markdown generator can render and
/// substitute placeholders into.
/// </summary>
public sealed record UserDocumentTemplate(
    string Slug,
    string Name,
    string Description,
    string Industry,
    string DefaultFormat,
    string Body,
    UserTemplateTier Tier,
    string SourcePath);

/// <summary>Origin tier for a user-authored template — drives override precedence and edit/delete affordances.</summary>
public enum UserTemplateTier
{
    /// <summary>Shipped with the install (assembly-relative <c>documents/</c> or <c>tools/</c>); read-only.</summary>
    BuiltIn,
    /// <summary>User-global (<c>~/.sovrant/documents</c> / <c>~/.sovrant/tools</c>); editable.</summary>
    Global,
    /// <summary>Project-local (<c>.sovrant/documents</c> / <c>.sovrant/tools</c>); editable.</summary>
    Project,
}
