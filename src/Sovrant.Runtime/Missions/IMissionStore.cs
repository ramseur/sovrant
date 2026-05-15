namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 51 — persistence seam for the mission layer. Wraps the V011
/// <c>missions</c> and <c>mission_events</c> tables. All writes go through
/// here so the cached mutable fields on <c>missions</c> and the immutable
/// journal on <c>mission_events</c> stay in lockstep.
/// </summary>
public interface IMissionStore
{
    /// <summary>
    /// Inserts a brand new mission in <see cref="MissionStatus.Planning"/> state
    /// and writes the corresponding <c>mission_created</c> event.
    /// </summary>
    Task<Mission> CreateAsync(
        string goal,
        string? sessionId = null,
        string? workspaceId = null,
        string? projectId = null,
        string? ownerUserId = null,
        CancellationToken ct = default);

    /// <summary>Returns the mission or <c>null</c> if no row matches.</summary>
    Task<Mission?> GetAsync(string missionId, CancellationToken ct = default);

    /// <summary>
    /// Returns missions visible to the given user (ownership match) or
    /// every mission if <paramref name="ownerUserId"/> is null. Ordered
    /// newest-first by <c>created_at</c>.
    /// </summary>
    Task<IReadOnlyList<Mission>> ListAsync(
        string? ownerUserId = null,
        MissionStatus? status = null,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the cached <see cref="Mission.Status"/>, <see cref="Mission.PlanJson"/>,
    /// and <see cref="Mission.CompletedAt"/> fields on the row. Does NOT
    /// write an event on its own — callers pair this with
    /// <see cref="AppendEventAsync"/> so the journal stays authoritative.
    /// </summary>
    Task UpdateStateAsync(
        string missionId,
        MissionStatus status,
        string? planJson = null,
        DateTimeOffset? completedAt = null,
        CancellationToken ct = default);

    /// <summary>Writes one row into <c>mission_events</c>.</summary>
    Task<MissionEvent> AppendEventAsync(
        string missionId,
        string eventType,
        string payloadJson,
        string? workspaceId = null,
        string? projectId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the event journal for a mission in insertion order. The
    /// caller can reconstruct every historical state by folding over this
    /// list without trusting the mutable <c>missions</c> row.
    /// </summary>
    Task<IReadOnlyList<MissionEvent>> GetEventsAsync(
        string missionId,
        CancellationToken ct = default);
}
