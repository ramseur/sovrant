using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// Run-level metadata stored as <c>_manifest.json</c> inside each run's
/// artifact directory. Contains the prompt, agent info, timestamps, and
/// file list for discoverability.
/// </summary>
public sealed class ArtifactManifest
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("workspace_id")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("project_id")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("files")]
    public Collection<string> Files { get; } = [];

    /// <summary>
    /// Present when the run was produced by a code scaffold tool (CodeCreate /
    /// CodeCreateMulti). Null for all other artifact types.
    /// </summary>
    [JsonPropertyName("code")]
    public CodeManifest? Code { get; set; }

    [JsonPropertyName("migrated_from")]
    public string? MigratedFrom { get; set; }

    [JsonPropertyName("original_slug")]
    public string? OriginalSlug { get; set; }
}

/// <summary>
/// Code-specific metadata written into <see cref="ArtifactManifest.Code"/> by
/// <c>CodeCreateTool</c> and <c>CodeCreateMultiTool</c> after scaffolding.
/// Provides the minimal information needed to build, run, and test the project
/// without reading individual files.
/// </summary>
public sealed class CodeManifest
{
    /// <summary>Template identifier used to scaffold, e.g. <c>dotnet/webapi</c>.</summary>
    [JsonPropertyName("template_id")]
    public string? TemplateId { get; set; }

    /// <summary>Primary language, e.g. <c>dotnet</c>, <c>node</c>, <c>python</c>.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>Project kind within the language, e.g. <c>webapi</c>, <c>cli</c>, <c>library</c>.</summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    /// <summary>Shell command to build the project from its root directory.</summary>
    [JsonPropertyName("build_command")]
    public string? BuildCommand { get; set; }

    /// <summary>Shell command to run the project after building.</summary>
    [JsonPropertyName("run_command")]
    public string? RunCommand { get; set; }

    /// <summary>Shell command to run the project's test suite.</summary>
    [JsonPropertyName("test_command")]
    public string? TestCommand { get; set; }

    /// <summary>
    /// Relative path to the primary entry point file or project descriptor,
    /// e.g. <c>MyApp/MyApp.csproj</c> or <c>package.json</c>.
    /// </summary>
    [JsonPropertyName("entry_point")]
    public string? EntryPoint { get; set; }
}
