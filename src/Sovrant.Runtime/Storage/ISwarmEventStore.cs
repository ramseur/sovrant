namespace Sovrant.Runtime.Storage;

/// <summary>
/// Persists swarm orchestration events. Replaces the JSONL-only
/// <c>~/.sovrant/swarm/sessions/{id}.jsonl</c> store from before Phase 37.5.
/// </summary>
/// <remarks>
/// The store works in terms of opaque <see cref="SwarmEventRecord"/>s — the
/// concrete event types live in <c>Sovrant.Agents.Swarm</c> and are serialized
/// to the <c>payload</c> column as JSON. This keeps <c>Sovrant.Runtime</c> from
/// taking a reverse dependency on <c>Sovrant.Agents</c>.
/// </remarks>
public interface ISwarmEventStore
{
    /// <summary>Records a single swarm event.</summary>
    Task RecordEventAsync(SwarmEventRecord record, CancellationToken ct = default);

    /// <summary>Returns all events for a swarm in insertion order.</summary>
    Task<IReadOnlyList<SwarmEventRecord>> LoadEventsAsync(string swarmId, CancellationToken ct = default);

    /// <summary>
    /// Returns the distinct swarm IDs that match the optional filter, ordered
    /// by most-recent-event timestamp first.
    /// </summary>
    Task<IReadOnlyList<string>> ListSwarmsAsync(SwarmListFilter? filter = null, CancellationToken ct = default);

    /// <summary>Returns whether any events exist for the given swarm ID.</summary>
    Task<bool> ExistsAsync(string swarmId, CancellationToken ct = default);

    /// <summary>Deletes all events for the given swarm ID. Returns rows deleted.</summary>
    Task<int> DeleteSwarmAsync(string swarmId, CancellationToken ct = default);

    /// <summary>
    /// Returns the <c>user_id</c> that owns the given swarm (read from the first
    /// recorded event), or <c>null</c> if the swarm does not exist or was created
    /// before V025 (pre-ownership tracking).
    /// </summary>
    Task<string?> GetOwnerAsync(string swarmId, CancellationToken ct = default);

    /// <summary>
    /// Returns the swarm IDs whose <c>parent_swarm_id</c> matches <paramref name="parentSwarmId"/>,
    /// ordered by most-recent-event timestamp first. Added in Phase 50 (V029).
    /// </summary>
    Task<IReadOnlyList<string>> ListChildrenAsync(string parentSwarmId, int? limit = null, CancellationToken ct = default);
}

/// <summary>
/// A single swarm event row. <see cref="Payload"/> is an opaque JSON document
/// produced by the caller (typically <c>JsonSerializer.Serialize</c> over the
/// concrete <c>SwarmEvent</c> subtype).
/// </summary>
public sealed record SwarmEventRecord(
    string SwarmId,
    string EventType,
    string Payload,
    DateTimeOffset Timestamp,
    string? AgentId = null,
    string? WorkspaceId = null,
    string? ProjectId = null,
    string? UserId = null,
    /// <summary>Populated when this swarm was spawned as a child in manager-led federation (Phase 50).</summary>
    string? ParentSwarmId = null);

/// <summary>Optional filter for <see cref="ISwarmEventStore.ListSwarmsAsync"/>.</summary>
public sealed record SwarmListFilter(
    string? WorkspaceId = null,
    string? ProjectId = null,
    int? Limit = null,
    /// <summary>When set, restricts results to child swarms of this parent (Phase 50).</summary>
    string? ParentSwarmId = null);
