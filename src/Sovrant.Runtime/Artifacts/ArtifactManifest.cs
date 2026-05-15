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

    [JsonPropertyName("migrated_from")]
    public string? MigratedFrom { get; set; }

    [JsonPropertyName("original_slug")]
    public string? OriginalSlug { get; set; }
}
