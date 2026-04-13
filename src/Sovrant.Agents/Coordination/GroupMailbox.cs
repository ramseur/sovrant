using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Coordination;

/// <summary>
/// Phase 57 — per-group facade over <see cref="ICoordinationEventStore"/>.
/// Translates between the domain-level <see cref="CoordinationEvent"/> and
/// the storage-level <see cref="CoordinationEventRecord"/>.
/// </summary>
public sealed class GroupMailbox
{
    private readonly string _groupId;
    private readonly string _workspaceId;
    private readonly string? _projectId;
    private readonly ICoordinationEventStore _store;

    public GroupMailbox(string groupId, string workspaceId, string? projectId, ICoordinationEventStore store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(store);
        _groupId = groupId;
        _workspaceId = workspaceId;
        _projectId = projectId;
        _store = store;
    }

    /// <summary>Posts a coordination event from this group to the store.</summary>
    public async Task<long> PostAsync(CoordinationEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var record = new CoordinationEventRecord(
            Id: 0,
            SourceGroupId: evt.SourceGroupId,
            TargetGroupId: evt.TargetGroupId,
            EventType: evt.EventType.ToString(),
            Payload: evt.Payload,
            Status: "pending",
            WorkspaceId: evt.WorkspaceId,
            ProjectId: evt.ProjectId,
            CreatedAt: string.Empty,
            DeliveredAt: null,
            AcknowledgedAt: null);

        return await _store.EnqueueAsync(record, ct).ConfigureAwait(false);
    }

    /// <summary>Drains all pending events targeted at this group, marking them delivered.</summary>
    public async Task<IReadOnlyList<CoordinationEvent>> DrainPendingAsync(CancellationToken ct = default)
    {
        var records = await _store.GetPendingAsync(_groupId, ct).ConfigureAwait(false);
        var events = new List<CoordinationEvent>(records.Count);

        foreach (var r in records)
        {
            await _store.MarkDeliveredAsync(r.Id, ct).ConfigureAwait(false);

            if (Enum.TryParse<CoordinationEventType>(r.EventType, ignoreCase: true, out var eventType))
            {
                events.Add(new CoordinationEvent(
                    r.SourceGroupId,
                    r.TargetGroupId,
                    eventType,
                    r.Payload,
                    r.WorkspaceId,
                    r.ProjectId));
            }
        }

        return events;
    }

    /// <summary>Acknowledges a previously delivered event.</summary>
    public async Task AcknowledgeAsync(long eventId, CancellationToken ct = default)
    {
        await _store.MarkAcknowledgedAsync(eventId, ct).ConfigureAwait(false);
    }

    /// <summary>Returns the number of pending events for this group.</summary>
    public async Task<int> PeekPendingCountAsync(CancellationToken ct = default)
    {
        return await _store.GetPendingCountAsync(_groupId, ct).ConfigureAwait(false);
    }
}
