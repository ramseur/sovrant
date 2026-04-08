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
    string? ProjectId = null);

/// <summary>Optional filter for <see cref="ISwarmEventStore.ListSwarmsAsync"/>.</summary>
public sealed record SwarmListFilter(
    string? WorkspaceId = null,
    string? ProjectId = null,
    int? Limit = null);
