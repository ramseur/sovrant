namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 57 — CRUD for the <c>group_pm_assignments</c> table that maps
/// each agent group to its PM agent template.
/// </summary>
public interface IGroupPMStore
{
    /// <summary>Assigns (upserts) a PM template to a group.</summary>
    Task UpsertAsync(GroupPMAssignment assignment, CancellationToken ct = default);

    /// <summary>Returns the PM assignment for a group, or <see langword="null"/> if not assigned.</summary>
    Task<GroupPMAssignment?> GetAsync(string groupId, CancellationToken ct = default);

    /// <summary>Returns all PM assignments in a workspace.</summary>
    Task<IReadOnlyList<GroupPMAssignment>> ListByWorkspaceAsync(string workspaceId, CancellationToken ct = default);

    /// <summary>Removes the PM assignment for a group.</summary>
    Task DeleteAsync(string groupId, CancellationToken ct = default);
}

/// <summary>Represents a row in the <c>group_pm_assignments</c> table.</summary>
public sealed record GroupPMAssignment(
    string GroupId,
    string GroupType,
    string PMTemplate,
    string WorkspaceId,
    string? ProjectId,
    string CreatedAt);
