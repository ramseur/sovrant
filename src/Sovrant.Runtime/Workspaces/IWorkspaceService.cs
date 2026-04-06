namespace Sovrant.Runtime.Workspaces;

/// <summary>
/// Service for workspace CRUD, membership, invites, configuration, usage, and memory.
/// </summary>
public interface IWorkspaceService
{
    // ── Workspace CRUD ─────────────────────────────────────────────────────

    /// <summary>Creates a personal workspace for a user. Called automatically on user creation.</summary>
    Task<Workspace> CreatePersonalWorkspaceAsync(string userId, CancellationToken ct = default);

    /// <summary>Creates a team workspace. The creator becomes the owner.</summary>
    Task<Workspace> CreateTeamWorkspaceAsync(string name, string slug, string ownerId, CancellationToken ct = default);

    /// <summary>Gets a workspace by ID.</summary>
    Task<Workspace?> GetAsync(string workspaceId, CancellationToken ct = default);

    /// <summary>Gets the personal workspace for a user.</summary>
    Task<Workspace?> GetPersonalAsync(string userId, CancellationToken ct = default);

    /// <summary>Lists workspaces the user belongs to (always includes personal).</summary>
    Task<IReadOnlyList<Workspace>> ListForUserAsync(string userId, CancellationToken ct = default);

    /// <summary>Updates workspace name/slug. Personal workspaces can be renamed but not deleted.</summary>
    Task<Workspace?> UpdateAsync(string workspaceId, string? name, string? slug, CancellationToken ct = default);

    /// <summary>Deletes a team workspace. Personal workspaces cannot be deleted.</summary>
    Task<bool> DeleteAsync(string workspaceId, CancellationToken ct = default);

    // ── Membership ─────────────────────────────────────────────────────────

    /// <summary>Checks if a user is a member of a workspace.</summary>
    Task<bool> IsMemberAsync(string workspaceId, string userId, CancellationToken ct = default);

    /// <summary>Gets a user's role in a workspace.</summary>
    Task<WorkspaceRole?> GetMemberRoleAsync(string workspaceId, string userId, CancellationToken ct = default);

    /// <summary>Lists members of a workspace.</summary>
    Task<IReadOnlyList<WorkspaceMember>> ListMembersAsync(string workspaceId, CancellationToken ct = default);

    /// <summary>Adds a member to a team workspace.</summary>
    Task AddMemberAsync(string workspaceId, string userId, WorkspaceRole role, CancellationToken ct = default);

    /// <summary>Removes a member from a team workspace. Cannot remove the owner.</summary>
    Task<bool> RemoveMemberAsync(string workspaceId, string userId, CancellationToken ct = default);

    // ── Invites ────────────────────────────────────────────────────────────

    /// <summary>Creates an invite to a team workspace.</summary>
    Task<WorkspaceInvite> CreateInviteAsync(string workspaceId, string email, WorkspaceRole role, CancellationToken ct = default);

    /// <summary>Accepts an invite by token. Adds the user as a member.</summary>
    Task<bool> AcceptInviteAsync(string token, string userId, CancellationToken ct = default);

    /// <summary>Deletes an invite.</summary>
    Task<bool> DeleteInviteAsync(string inviteId, CancellationToken ct = default);

    /// <summary>Lists pending invites for a workspace.</summary>
    Task<IReadOnlyList<WorkspaceInvite>> ListInvitesAsync(string workspaceId, CancellationToken ct = default);

    // ── Config ─────────────────────────────────────────────────────────────

    /// <summary>Gets all config key-value pairs for a workspace.</summary>
    Task<IReadOnlyDictionary<string, string>> GetConfigAsync(string workspaceId, CancellationToken ct = default);

    /// <summary>Sets config key-value pairs for a workspace.</summary>
    Task SetConfigAsync(string workspaceId, IReadOnlyDictionary<string, string> values, CancellationToken ct = default);

    // ── Usage ──────────────────────────────────────────────────────────────

    /// <summary>Gets aggregated token usage for a workspace.</summary>
    Task<WorkspaceUsage> GetUsageAsync(string workspaceId, CancellationToken ct = default);

    // ── Memory ─────────────────────────────────────────────────────────────

    /// <summary>Lists workspace-scoped memory entries.</summary>
    Task<IReadOnlyList<WorkspaceMemoryEntry>> ListMemoryAsync(string workspaceId, string? layer = null, CancellationToken ct = default);

    /// <summary>Saves a workspace memory entry.</summary>
    Task SaveMemoryAsync(WorkspaceMemoryEntry entry, CancellationToken ct = default);

    /// <summary>Deletes a workspace memory entry.</summary>
    Task<bool> DeleteMemoryAsync(string memoryId, CancellationToken ct = default);
}

/// <summary>Aggregated token usage for a workspace.</summary>
public sealed record WorkspaceUsage
{
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public double TotalCostUsd { get; init; }
    public int SessionCount { get; init; }
}
