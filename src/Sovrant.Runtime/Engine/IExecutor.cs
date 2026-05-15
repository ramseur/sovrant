namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — runs <see cref="RuntimePlan"/> steps and reports outcomes.
/// The executor is the one component that actually calls tools; the planner
/// never does. This gives the engine a clean seam: any side effect has to
/// pass through the executor, which means any side effect also passes
/// through the <c>runtime_traces</c> write.
///
/// The executor is crash-safe by construction: every state transition
/// (step started / step completed / re-plan requested) is committed to
/// the trace store *before* the side effect runs. On process restart,
/// <c>EngineRecovery</c> (Phase 51 step G) walks the trace and resumes
/// any executor that was mid-step.
/// </summary>
public interface IExecutor
{
    /// <summary>
    /// Runs a plan to completion, requesting re-plans via <paramref name="replanner"/>
    /// when steps contradict expectations or fail repeatedly. Returns the
    /// final plan version (may differ from the input if re-plans occurred)
    /// and the ordered list of step outcomes for *all* steps that actually
    /// ran, across all plan versions.
    /// </summary>
    /// <param name="plan">The plan to execute. May be replaced mid-run by a re-plan.</param>
    /// <param name="runContext">Ambient run metadata — the runtime run id used for trace attribution, plus the owning session/workspace/project.</param>
    /// <param name="replanner">Callback invoked when the executor decides a re-plan is required. Typically delegates to <see cref="IPlanner.ReplanAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ExecutionResult> ExecuteAsync(
        RuntimePlan plan,
        EngineRunContext runContext,
        Replanner replanner,
        CancellationToken ct = default);
}

/// <summary>
/// Delegate shape for the re-plan callback. Kept as a delegate rather than
/// an <see cref="IPlanner"/> reference so the executor can be tested with
/// a simple lambda that returns a canned patched plan.
/// </summary>
public delegate Task<RuntimePlan> Replanner(
    RuntimePlan currentPlan,
    IReadOnlyList<StepOutcome> completedOutcomes,
    ReplanTrigger trigger,
    CancellationToken ct);

/// <summary>
/// Terminal result of an <see cref="IExecutor.ExecuteAsync"/> call.
/// </summary>
/// <param name="FinalPlan">The plan that was active when execution completed. Differs from the input plan if one or more re-plans ran.</param>
/// <param name="Outcomes">Outcomes for every step that actually ran, in execution order, across all plan versions.</param>
/// <param name="ReplanCount">Number of re-plans that occurred during this run. Bounded by the executor's configured re-plan depth.</param>
/// <param name="TerminalState">Why execution ended — all steps ran, a fatal error, or the caller cancelled.</param>
public sealed record ExecutionResult(
    RuntimePlan FinalPlan,
    IReadOnlyList<StepOutcome> Outcomes,
    int ReplanCount,
    ExecutionTerminalState TerminalState);

/// <summary>Why an <see cref="IExecutor.ExecuteAsync"/> call returned.</summary>
public enum ExecutionTerminalState
{
    /// <summary>Every step in the final plan ran and produced an outcome (success or skip).</summary>
    Completed,

    /// <summary>A step failed and the executor exhausted its re-plan budget without recovering.</summary>
    FailedAfterReplans,

    /// <summary>The caller cancelled the <see cref="CancellationToken"/>.</summary>
    Cancelled,

    /// <summary>Execution is paused awaiting a human decision. Only used by the mission layer; the bare engine never returns this.</summary>
    AwaitingHuman,
}
