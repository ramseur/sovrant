namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — inspects <c>runtime_traces</c> at process startup and
/// closes out any run that was mid-step when the previous process
/// exited. The crash-safety rule the executor follows is "write the
/// state transition before the side effect", which means a crash
/// between <c>step_started</c> and <c>step_completed</c>/<c>step_failed</c>
/// leaves exactly one orphaned <c>step_started</c> row. EngineRecovery
/// finds those rows and writes the missing close-out entries so the
/// trace is internally consistent and the run no longer shows up in
/// <see cref="Storage.IRuntimeTraceStore.ListInFlightRunsAsync"/>.
///
/// This is deliberately *not* "resume the run where it left off": true
/// resumption would require persisting the in-memory plan and outcome
/// list, which Phase 51 does not do. What it does give you is an
/// honest trace log (every step has a start/end pair) and a clear
/// signal to the UI/CLI that "your last run crashed, here is its
/// partial outcome list" without the engine silently reusing stale
/// state on the next prompt.
/// </summary>
public interface IEngineRecovery
{
    /// <summary>
    /// Scans for in-flight runs and closes them out. For each affected
    /// run, writes a synthetic <c>step_failed</c> entry for every
    /// orphaned <c>step_started</c> and a <c>run_completed</c> entry
    /// with terminal state <c>Cancelled</c>. Returns one
    /// <see cref="RecoveredRun"/> per run that was closed out.
    /// </summary>
    Task<IReadOnlyList<RecoveredRun>> RecoverAsync(CancellationToken ct = default);
}

/// <summary>
/// A run that EngineRecovery closed out at startup. The caller decides
/// what to do with the information — the bare CLI logs it and moves
/// on; the mission layer surfaces it as an AwaitingHuman state.
/// </summary>
/// <param name="RuntimeRunId">The run id that was in-flight when the previous process exited.</param>
/// <param name="PlanId">Plan id active when the crash happened.</param>
/// <param name="PlanVersion">Plan version active when the crash happened.</param>
/// <param name="OrphanedStepIndexes">Step indexes that had a <c>step_started</c> without a matching <c>step_completed</c>/<c>step_failed</c>. These are the steps recovery synthesised a failure for.</param>
/// <param name="SessionId">Session the run belonged to, if the trace row carried it.</param>
/// <param name="WorkspaceId">Workspace the run belonged to, if the trace row carried it.</param>
/// <param name="ProjectId">Project the run belonged to, if the trace row carried it.</param>
public sealed record RecoveredRun(
    string RuntimeRunId,
    string PlanId,
    int PlanVersion,
    IReadOnlyList<int> OrphanedStepIndexes,
    string? SessionId = null,
    string? WorkspaceId = null,
    string? ProjectId = null);
