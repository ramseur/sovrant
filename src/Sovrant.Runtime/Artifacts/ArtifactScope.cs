namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// Identifies the workspace-scoped location where artifacts for a given run
/// are stored. The workspace is the top-level partition — all users in the
/// same workspace share the same artifact tree. The initiating user is
/// tracked in the <see cref="ArtifactManifest"/> metadata, not in the
/// directory path.
/// </summary>
/// <remarks>
/// Layout: <c>{root}/{workspace}/{project}/{run}/</c>
/// Unknown segments fall back to sentinel values so a fresh install with
/// no workspaces or projects configured still works.
/// </remarks>
public sealed record ArtifactScope
{
    /// <summary>Sentinel used when no workspace is configured (matches Phase 35 seed).</summary>
    public const string DefaultWorkspaceId = "personal";

    /// <summary>Sentinel used when no project is configured.</summary>
    public const string DefaultProjectId = "default-project";

    /// <summary>The workspace. Defaults to <see cref="DefaultWorkspaceId"/>.</summary>
    public string WorkspaceId { get; init; } = DefaultWorkspaceId;

    /// <summary>The project within the workspace. Defaults to <see cref="DefaultProjectId"/>.</summary>
    public string ProjectId { get; init; } = DefaultProjectId;

    /// <summary>
    /// The run (session) ID. Required for write operations; optional for
    /// list/delete at higher scope levels.
    /// </summary>
    public string? RunId { get; init; }

    /// <summary>
    /// The user who initiated this run. Stored in the manifest for
    /// attribution but <b>not</b> part of the directory path — all
    /// workspace members see the same artifacts.
    /// </summary>
    public string? UserId { get; init; }
}
