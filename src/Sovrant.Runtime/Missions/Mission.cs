namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 51 — a Sovrant "mission": a long-lived goal the system pursues
/// autonomously, potentially over many engine runs, with re-planning,
/// acceptance gates, and an append-only event journal. Sits *on top* of
/// the Phase 51 engine layer: one mission step eventually becomes one
/// <c>IExecutor.ExecuteAsync</c> call with its own runtime run id and
/// trace rows.
///
/// The mutable fields on this record (<see cref="Status"/>, <see cref="PlanJson"/>,
/// <see cref="UpdatedAt"/>, <see cref="CompletedAt"/>) are a convenience
/// cache — the canonical history lives in <see cref="MissionEvent"/> rows,
/// so any state transition also writes an event.
/// </summary>
public sealed record Mission(
    string Id,
    string Goal,
    MissionStatus Status,
    string PlanJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt = null,
    string? SessionId = null,
    string? WorkspaceId = null,
    string? ProjectId = null,
    string? OwnerUserId = null,
    bool IsPrivate = true);

/// <summary>
/// Lifecycle state of a <see cref="Mission"/>. Corresponds 1:1 with the
/// <c>status</c> column values in V011.
/// </summary>
public enum MissionStatus
{
    /// <summary>Just created — planner hasn't produced a plan yet, or a re-plan is in progress.</summary>
    Planning,

    /// <summary>An engine run is currently executing a mission step.</summary>
    Running,

    /// <summary>Execution paused pending a human acceptance decision.</summary>
    AwaitingHuman,

    /// <summary>Mission reached its acceptance criteria and is done.</summary>
    Completed,

    /// <summary>Mission exhausted its re-plan budget or hit a fatal error.</summary>
    Failed,

    /// <summary>A human or supervisor cancelled the mission before it completed.</summary>
    Cancelled,
}

/// <summary>
/// One row in the mission event journal. Every mutation to a mission
/// (create, plan revised, run started, run completed, acceptance decision,
/// pause, resume, terminal state) writes one of these rows so the mission
/// history is fully reconstructable from the journal alone.
/// </summary>
public sealed record MissionEvent(
    long Id,
    string MissionId,
    string EventType,
    string PayloadJson,
    DateTimeOffset Timestamp,
    string? WorkspaceId = null,
    string? ProjectId = null);

/// <summary>
/// Canonical event-type strings written to <see cref="MissionEvent.EventType"/>.
/// These match the comment header on <c>mission_events</c> in V011 so
/// downstream consumers (UI, pm_export) can match on a stable vocabulary.
/// </summary>
public static class MissionEventTypes
{
    public const string MissionCreated = "mission_created";
    public const string PlanRevised = "plan_revised";
    public const string RunStarted = "run_started";
    public const string RunCompleted = "run_completed";
    public const string AcceptanceApproved = "acceptance_approved";
    public const string AcceptanceRejected = "acceptance_rejected";
    public const string Paused = "paused";
    public const string Resumed = "resumed";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}
