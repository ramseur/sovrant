namespace Sovrant.Agents.Coordination;

/// <summary>
/// Phase 57 — a typed coordination event exchanged between agent groups
/// via their PM agents.
/// </summary>
public sealed record CoordinationEvent(
    string SourceGroupId,
    string TargetGroupId,
    CoordinationEventType EventType,
    string Payload,
    string WorkspaceId,
    string? ProjectId = null);

/// <summary>Discriminator for the kind of coordination message.</summary>
public enum CoordinationEventType
{
    /// <summary>A blocking dependency that prevents progress.</summary>
    Blocker,

    /// <summary>An informational update about group progress.</summary>
    Update,

    /// <summary>A request for information or action from another group.</summary>
    Request,

    /// <summary>A handoff of responsibility from one group to another.</summary>
    Handoff,
}
