namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 51 — persistence seam for the workflow layer. Wraps the V011
/// <c>missions</c> and <c>mission_events</c> tables. All writes go through
/// here so the cached mutable fields on <c>missions</c> and the immutable
/// journal on <c>mission_events</c> stay in lockstep.
/// </summary>
public interface IWorkflowStore
{
    /// <summary>
    /// Inserts a brand new workflow in <see cref="WorkflowStatus.Planning"/> state
    /// and writes the corresponding <c>mission_created</c> event.
    /// </summary>
    Task<Workflow> CreateAsync(
        string goal,
        string? sessionId = null,
        string? workspaceId = null,
        string? projectId = null,
        string? ownerUserId = null,
        CancellationToken ct = default);

    /// <summary>Returns the workflow or <c>null</c> if no row matches.</summary>
    Task<Workflow?> GetAsync(string workflowId, CancellationToken ct = default);

    /// <summary>
    /// Returns missions visible to the given user (ownership match) or
    /// every workflow if <paramref name="ownerUserId"/> is null. Ordered
    /// newest-first by <c>created_at</c>.
    /// </summary>
    Task<IReadOnlyList<Workflow>> ListAsync(
        string? ownerUserId = null,
        WorkflowStatus? status = null,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the cached <see cref="Workflow.Status"/>, <see cref="Workflow.PlanJson"/>,
    /// and <see cref="Workflow.CompletedAt"/> fields on the row. Does NOT
    /// write an event on its own — callers pair this with
    /// <see cref="AppendEventAsync"/> so the journal stays authoritative.
    /// </summary>
    Task UpdateStateAsync(
        string workflowId,
        WorkflowStatus status,
        string? planJson = null,
        DateTimeOffset? completedAt = null,
        CancellationToken ct = default);

    /// <summary>Writes one row into <c>mission_events</c>.</summary>
    Task<WorkflowEvent> AppendEventAsync(
        string workflowId,
        string eventType,
        string payloadJson,
        string? workspaceId = null,
        string? projectId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the event journal for a workflow in insertion order. The
    /// caller can reconstruct every historical state by folding over this
    /// list without trusting the mutable <c>missions</c> row.
    /// </summary>
    Task<IReadOnlyList<WorkflowEvent>> GetEventsAsync(
        string workflowId,
        CancellationToken ct = default);

    /// <summary>
    /// Phase 99 — flips the privacy flag on a workflow. The owner predicate
    /// is enforced inside the UPDATE so a mismatched owner is a silent
    /// no-op (zero rows affected) and never leaks the row's existence.
    /// </summary>
    Task UpdatePrivacyAsync(
        string workflowId,
        string ownerUserId,
        bool isPrivate,
        CancellationToken ct = default);
}
