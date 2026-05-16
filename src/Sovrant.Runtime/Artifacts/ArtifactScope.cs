using Sovrant.Runtime.Workspaces;

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
    /// <summary>Sentinel used when no project is configured.</summary>
    public const string DefaultProjectId = "default-project";

    /// <summary>
    /// The workspace. Defaults to the active user's personal workspace
    /// (<c>ws-personal-{userId}</c>) — never the bare <c>"personal"</c>
    /// literal. Phase 87 unifies this with the canonical workspace id
    /// produced by <see cref="SqliteWorkspaceStore"/>.
    /// </summary>
    public string WorkspaceId { get; init; } = WorkspaceIdentity.DefaultPersonal();

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

    /// <summary>
    /// Human-readable workspace name used to suffix the workspace directory:
    /// <c>{WorkspaceId}__{WorkspaceName}</c>. Optional — falls back to bare ID.
    /// </summary>
    public string? WorkspaceName { get; init; }

    /// <summary>
    /// Human-readable project name used to suffix the project directory:
    /// <c>{ProjectId}__{ProjectName}</c>. Optional — falls back to bare ID.
    /// </summary>
    public string? ProjectName { get; init; }

    /// <summary>
    /// Returns the canonical personal workspace id for the given user.
    /// Prefer this over inlining the format string.
    /// </summary>
    public static string DefaultWorkspaceFor(string userId)
        => WorkspaceIdentity.DefaultPersonalFor(userId);
}
