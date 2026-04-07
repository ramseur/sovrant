using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Projects;

/// <summary>
/// Service for project CRUD, membership, configuration, sessions, usage, and memory.
/// Projects belong to exactly one workspace and inherit its isolation boundary.
/// </summary>
public interface IProjectService
{
    // ── Project CRUD ───────────────────────────────────────────────────────

    /// <summary>Creates a project within a workspace.</summary>
    Task<Project> CreateAsync(string workspaceId, string name, string slug, string? description = null, CancellationToken ct = default);

    /// <summary>Gets a project by ID.</summary>
    Task<Project?> GetAsync(string projectId, CancellationToken ct = default);

    /// <summary>Lists projects within a workspace.</summary>
    Task<IReadOnlyList<Project>> ListAsync(string workspaceId, bool includeArchived = false, CancellationToken ct = default);

    /// <summary>Updates project name, slug, or description.</summary>
    Task<Project?> UpdateAsync(string projectId, string? name = null, string? slug = null, string? description = null, CancellationToken ct = default);

    /// <summary>Archives a project (soft-delete). Sets archived_at timestamp.</summary>
    Task<bool> ArchiveAsync(string projectId, CancellationToken ct = default);

    /// <summary>Unarchives a project. Clears archived_at timestamp.</summary>
    Task<bool> UnarchiveAsync(string projectId, CancellationToken ct = default);

    /// <summary>Permanently deletes a project and all associated data.</summary>
    Task<bool> DeleteAsync(string projectId, CancellationToken ct = default);

    // ── Membership ─────────────────────────────────────────────────────────

    /// <summary>Lists project members. If no explicit members, returns empty (all workspace members have access).</summary>
    Task<IReadOnlyList<ProjectMember>> ListMembersAsync(string projectId, CancellationToken ct = default);

    /// <summary>Adds a member to a project. User must be a workspace member.</summary>
    Task<bool> AddMemberAsync(string projectId, string userId, ProjectRole role, CancellationToken ct = default);

    /// <summary>Removes a member from a project.</summary>
    Task<bool> RemoveMemberAsync(string projectId, string userId, CancellationToken ct = default);

    /// <summary>Checks if a user has access to a project (explicit member, or no explicit members = open).</summary>
    Task<bool> HasAccessAsync(string projectId, string userId, CancellationToken ct = default);

    // ── Config ─────────────────────────────────────────────────────────────

    /// <summary>Gets project-scoped config key-value pairs.</summary>
    Task<IReadOnlyDictionary<string, string>> GetConfigAsync(string projectId, CancellationToken ct = default);

    /// <summary>Gets resolved config with inheritance: project → workspace → global defaults.</summary>
    Task<IReadOnlyDictionary<string, string>> GetResolvedConfigAsync(string projectId, CancellationToken ct = default);

    /// <summary>Sets project-scoped config key-value pairs.</summary>
    Task SetConfigAsync(string projectId, IReadOnlyDictionary<string, string> values, CancellationToken ct = default);

    // ── Sessions ───────────────────────────────────────────────────────────

    /// <summary>Lists session IDs scoped to a project.</summary>
    Task<IReadOnlyList<string>> ListSessionsAsync(string projectId, CancellationToken ct = default);

    // ── Usage ──────────────────────────────────────────────────────────────

    /// <summary>Gets aggregated token usage for a project.</summary>
    Task<ProjectUsage> GetUsageAsync(string projectId, CancellationToken ct = default);

    // ── Memory ─────────────────────────────────────────────────────────────

    /// <summary>Lists project-scoped memory + inherited workspace memory.</summary>
    Task<IReadOnlyList<WorkspaceMemoryEntry>> GetMemoryAsync(string projectId, string? layer = null, CancellationToken ct = default);
}
