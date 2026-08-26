using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Artifacts;

/// <summary>
/// Identifies the workspace- and project-scoped location where artifacts for a given
/// run are stored. The workspace is the top-level tenant object — all users in the
/// same workspace share the same artifact tree. The initiating user is tracked in the
/// <see cref="ArtifactManifest"/> metadata, not in the path.
/// </summary>
/// <remarks>
/// Projects-only layout: <c>{root}/{workspace}/projects/{project}/artifacts/{run}/</c>.
/// Every artifact belongs to a workspace AND a project — there is no workspace-level
/// (project-less) storage mode. A session with no explicit project uses
/// <see cref="DefaultProjectId"/>, which is a real project folder like any other.
/// </remarks>
public sealed record ArtifactScope
{
    /// <summary>
    /// Project ID used when the caller has no explicit project selected. This is a
    /// real project folder in the artifact tree, not a workspace-level bypass —
    /// artifacts always nest under <c>projects/{ProjectId}/artifacts/</c>.
    /// </summary>
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
