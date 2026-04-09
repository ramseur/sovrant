namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 51 — durable store for the engine's structured reasoning trace.
/// Every state transition made by an <c>IExecutor</c> is written here
/// <i>before</i> the corresponding side effect runs. On process restart,
/// <c>EngineRecovery</c> walks this table to resume any executor that was
/// mid-step, and the re-planner reads the trace when deciding which steps
/// to rewrite.
///
/// The store is intentionally narrow: it persists opaque JSON payloads
/// produced by the engine layer. Concrete plan / step / outcome shapes
/// live in <c>Sovrant.Runtime.Engine</c> and are serialised here, the
/// same way <see cref="ISwarmEventStore"/> keeps swarm events decoupled
/// from the <c>Sovrant.Agents</c> concrete types.
/// </summary>
public interface IRuntimeTraceStore
{
    /// <summary>
    /// Appends a single trace entry. Called by the executor before the
    /// side effect of the entry (e.g. a tool call) actually runs, so a
    /// crash mid-side-effect leaves a recoverable trail.
    /// </summary>
    Task AppendAsync(RuntimeTraceEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Loads every trace entry for a runtime run in insertion order.
    /// Used by the re-planner and by <c>EngineRecovery</c> on startup.
    /// </summary>
    Task<IReadOnlyList<RuntimeTraceEntry>> LoadAsync(string runtimeRunId, CancellationToken ct = default);

    /// <summary>
    /// Returns runtime run IDs that have at least one entry with
    /// <see cref="RuntimeTraceEntry.EntryType"/> equal to
    /// <c>"step_started"</c> and no matching <c>"step_completed"</c> /
    /// <c>"step_failed"</c> entry. These are the runs that need recovery.
    /// </summary>
    Task<IReadOnlyList<string>> ListInFlightRunsAsync(CancellationToken ct = default);

    /// <summary>Deletes every trace entry for a runtime run. Returns rows deleted.</summary>
    Task<int> DeleteRunAsync(string runtimeRunId, CancellationToken ct = default);
}

/// <summary>
/// One row in <c>runtime_traces</c>. Entries form an append-only log per
/// <paramref name="RuntimeRunId"/>; the engine replays them on recovery
/// to reconstruct where a crashed executor was in its plan.
/// </summary>
/// <param name="RuntimeRunId">Opaque identifier for one engine run. A mission step that invokes the engine creates one run id; a bare CLI prompt creates a different one.</param>
/// <param name="PlanId">The <see cref="Engine.RuntimePlan.Id"/> active when this entry was written.</param>
/// <param name="PlanVersion">The plan version active when this entry was written; lets the replayer tell the difference between "before re-plan" and "after re-plan" entries that share the same step index.</param>
/// <param name="StepIndex">Index of the step this entry relates to, or <c>null</c> for plan-level entries (<c>plan_created</c>, <c>replan_requested</c>).</param>
/// <param name="EntryType">Short discriminator — <c>plan_created</c>, <c>step_started</c>, <c>step_completed</c>, <c>step_failed</c>, <c>replan_requested</c>, <c>run_completed</c>, etc.</param>
/// <param name="Payload">Opaque JSON payload. Schema is owned by the engine layer.</param>
/// <param name="Timestamp">Wall-clock time when the entry was appended.</param>
/// <param name="SessionId">Session that owns the runtime run, for audit joins.</param>
/// <param name="WorkspaceId">Workspace the run belongs to, for per-workspace cleanup and quotas.</param>
/// <param name="ProjectId">Project the run belongs to, if any.</param>
public sealed record RuntimeTraceEntry(
    string RuntimeRunId,
    string PlanId,
    int PlanVersion,
    int? StepIndex,
    string EntryType,
    string Payload,
    DateTimeOffset Timestamp,
    string? SessionId = null,
    string? WorkspaceId = null,
    string? ProjectId = null);
