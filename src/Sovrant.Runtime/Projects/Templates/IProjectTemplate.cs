namespace Sovrant.Runtime.Projects.Templates;

/// <summary>
/// Phase 73 — A language-and-kind-scoped project scaffold. Given a
/// <see cref="ScaffoldContext"/>, produces the full file tree needed to
/// initialise a working project: source, config, dependencies, tests, README.
/// </summary>
/// <remarks>
/// Parallel to <c>IDocumentTemplate</c> (Phase 66) and the agent template
/// pattern (<c>FileSystemTemplateLoader</c>). Templates are pure functions —
/// they return <see cref="ProjectFile"/> records; the caller (<c>CodeCreateTool</c>)
/// decides how and where to write them via <c>write_many</c>.
/// </remarks>
public interface IProjectTemplate
{
    /// <summary>
    /// Canonical identifier in <c>{language}/{kind}</c> form, e.g.
    /// <c>node/express-api</c>, <c>dotnet/webapi</c>, <c>python/fastapi</c>.
    /// </summary>
    string Id { get; }

    /// <summary>Language key that groups templates in the registry, e.g. <c>node</c>, <c>dotnet</c>, <c>python</c>.</summary>
    string Language { get; }

    /// <summary>Project kind within the language, e.g. <c>express-api</c>, <c>cli</c>, <c>library</c>.</summary>
    string Kind { get; }

    /// <summary>Human-readable name for pickers and prompts, e.g. <c>Node.js Express API</c>.</summary>
    string Name { get; }

    /// <summary>One-line description of what the scaffold produces.</summary>
    string Description { get; }

    /// <summary>
    /// Parameters the scaffold requires or accepts beyond the mandatory
    /// <see cref="ScaffoldContext.ProjectName"/>. The <c>CodeCreateTool</c>
    /// collects missing required parameters via conversation before calling
    /// <see cref="Scaffold"/>.
    /// </summary>
    IReadOnlyList<ScaffoldParameter> Parameters { get; }

    /// <summary>
    /// Optional override for the shell command that builds the project.
    /// When null, <c>ScaffoldCommands.For()</c> provides the language default.
    /// </summary>
    string? BuildCommand => null;

    /// <summary>
    /// Optional override for the shell command that runs the project.
    /// When null, <c>ScaffoldCommands.For()</c> provides the language default.
    /// </summary>
    string? RunCommand => null;

    /// <summary>
    /// Optional override for the shell command that runs tests.
    /// When null, <c>ScaffoldCommands.For()</c> provides the language default.
    /// </summary>
    string? TestCommand => null;

    /// <summary>
    /// Optional override for the primary entry-point path relative to the project root.
    /// When null, <c>ScaffoldCommands.EntryPoint()</c> provides the language default.
    /// </summary>
    string? EntryPoint => null;

    /// <summary>
    /// Produces the complete file tree for the project.
    /// All paths in the returned <see cref="ProjectFile"/> list are relative
    /// to the project root (e.g. <c>src/index.ts</c>, <c>README.md</c>).
    /// </summary>
    IReadOnlyList<ProjectFile> Scaffold(ScaffoldContext context);
}

/// <summary>
/// Everything a template needs to produce its file tree.
/// <see cref="ProjectName"/> is always present; extra template-specific
/// values arrive via <see cref="Extra"/>.
/// </summary>
public sealed record ScaffoldContext(
    string ProjectName,
    IReadOnlyDictionary<string, string>? Extra = null)
{
    /// <summary>Returns the value of an extra parameter, or <paramref name="fallback"/> if absent.</summary>
    public string Get(string key, string fallback = "") =>
        Extra is not null && Extra.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>Returns true when the extra parameter is present and non-empty.</summary>
    public bool Has(string key) =>
        Extra is not null && Extra.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v);
}

/// <summary>A single file produced by a scaffold.</summary>
public sealed record ProjectFile(
    string RelativePath,
    string Content,
    bool IsExecutable = false);

/// <summary>
/// Describes a parameter a template accepts beyond the project name.
/// Used by <c>CodeCreateTool</c> to collect missing values via conversation.
/// </summary>
public sealed record ScaffoldParameter(
    string Name,
    string Description,
    bool Required = false,
    string? Default = null);
