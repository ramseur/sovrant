namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — the narrow seam between <see cref="IExecutor"/> (which owns
/// the re-plan loop, the trace log, and the retry policy) and whatever
/// code actually dispatches a step's work to a model or a tool.
///
/// Splitting this out lets the re-plan loop be unit-tested against a fake
/// step runner that returns canned outcomes, without having to stand up
/// <c>ConversationRuntime</c>, <c>SmartRouter</c>, or the tool registry.
/// The production implementation — which does talk to the runtime and the
/// router — is introduced in Phase 51 step I.
/// </summary>
public interface IStepRunner
{
    /// <summary>
    /// Runs a single <see cref="RuntimeStep"/> and returns its outcome.
    /// Implementations must not write to <c>runtime_traces</c> themselves —
    /// that is the executor's job, and it happens on both sides of this call
    /// so a crash inside a step runner is still recoverable.
    /// </summary>
    Task<StepOutcome> RunAsync(RuntimeStep step, StepExecutionContext context, CancellationToken ct);
}

/// <summary>
/// Ambient context handed to an <see cref="IStepRunner"/> so it knows
/// which engine run, plan version, and session it is executing under.
/// Also carries the accumulated outcome history so a step runner can
/// make decisions based on what has already happened in this run.
/// </summary>
/// <param name="RuntimeRunId">Engine run identifier — matches the <c>runtime_traces.runtime_run_id</c> column the executor writes on either side of this call.</param>
/// <param name="PlanId">Current plan family id.</param>
/// <param name="PlanVersion">Current plan version (1 for the initial plan; incremented on every re-plan).</param>
/// <param name="SessionId">Owning session. Used for tool/provider dispatch in the production step runner.</param>
/// <param name="WorkspaceId">Owning workspace, if any.</param>
/// <param name="ProjectId">Owning project, if any.</param>
/// <param name="AttemptNumber">1 on the first attempt at this step, 2 on the first retry, and so on. The executor increments this before calling back in.</param>
/// <param name="PriorOutcomes">Outcomes already recorded in this run (across all plan versions), oldest first. Read-only.</param>
public sealed record StepExecutionContext(
    string RuntimeRunId,
    string PlanId,
    int PlanVersion,
    string SessionId,
    string? WorkspaceId,
    string? ProjectId,
    int AttemptNumber,
    IReadOnlyList<StepOutcome> PriorOutcomes);
