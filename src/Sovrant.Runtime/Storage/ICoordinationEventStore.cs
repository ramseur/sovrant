namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 57 — persists coordination events exchanged between agent groups
/// via their PM agents. Events flow through the <c>coordination_events</c>
/// table (V013) and follow a pending → delivered → acknowledged lifecycle.
/// </summary>
public interface ICoordinationEventStore
{
    /// <summary>Enqueues a new coordination event and returns its auto-generated ID.</summary>
    Task<long> EnqueueAsync(CoordinationEventRecord record, CancellationToken ct = default);

    /// <summary>Returns all pending (undelivered) events targeted at <paramref name="targetGroupId"/>.</summary>
    Task<IReadOnlyList<CoordinationEventRecord>> GetPendingAsync(string targetGroupId, CancellationToken ct = default);

    /// <summary>Marks an event as delivered (sets <c>delivered_at</c>).</summary>
    Task MarkDeliveredAsync(long eventId, CancellationToken ct = default);

    /// <summary>Marks an event as acknowledged (sets <c>acknowledged_at</c>).</summary>
    Task MarkAcknowledgedAsync(long eventId, CancellationToken ct = default);

    /// <summary>Returns recent events where <paramref name="groupId"/> is source or target.</summary>
    Task<IReadOnlyList<CoordinationEventRecord>> ListByGroupAsync(string groupId, int limit = 50, CancellationToken ct = default);

    /// <summary>Returns the number of pending events targeted at <paramref name="targetGroupId"/>.</summary>
    Task<int> GetPendingCountAsync(string targetGroupId, CancellationToken ct = default);
}

/// <summary>Represents a row in the <c>coordination_events</c> table.</summary>
public sealed record CoordinationEventRecord(
    long Id,
    string SourceGroupId,
    string TargetGroupId,
    string EventType,
    string Payload,
    string Status,
    string WorkspaceId,
    string? ProjectId,
    string CreatedAt,
    string? DeliveredAt,
    string? AcknowledgedAt);
