namespace Sovrant.Tools.Templates;

/// <summary>
/// A user-authored markdown tool template loaded from disk. Distinct from
/// <see cref="ITool"/> — these are <em>instructional</em> tool guides
/// (descriptions, usage notes, examples, prompts) shown in the Knowledge UI.
/// They do not register handlers with <see cref="Sovrant.Runtime.Tools.IToolRegistry"/>.
/// </summary>
public sealed record UserToolTemplate(
    string Slug,
    string Name,
    string Description,
    string Category,
    string Body,
    UserToolTier Tier,
    string SourcePath);

/// <summary>Origin tier for a user-authored tool template.</summary>
public enum UserToolTier
{
    BuiltIn,
    Global,
    Project,
}
