namespace Sovrant.Agents.Coordination;

/// <summary>
/// Phase 57 — interface for a PM (Project Manager) agent that coordinates
/// inter-group communication on behalf of a single agent group.
/// </summary>
public interface IPMAgent
{
    /// <summary>The group this PM agent manages.</summary>
    string GroupId { get; }

    /// <summary>
    /// Called when an agent in this group produces an update. The PM decides
    /// whether to broadcast coordination events to other groups.
    /// </summary>
    Task<IReadOnlyList<CoordinationEvent>> OnGroupUpdateAsync(
        string agentName,
        string updateSummary,
        CancellationToken ct = default);

    /// <summary>
    /// Called when coordination events arrive from other groups.
    /// The PM processes them and adds them to its briefing buffer.
    /// </summary>
    Task ReceiveCoordinationAsync(
        IReadOnlyList<CoordinationEvent> events,
        CancellationToken ct = default);

    /// <summary>
    /// Called at the end of a wave or cycle. The PM reviews its accumulated
    /// briefing buffer and decides what to broadcast to other groups.
    /// </summary>
    Task<IReadOnlyList<CoordinationEvent>> DecideBroadcastAsync(
        CancellationToken ct = default);
}
