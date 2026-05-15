namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — produces and patches <see cref="RuntimePlan"/>s. The default
/// implementation calls a high-tier LLM with a structured-output schema;
/// tests and deterministic scripting use a fake planner that returns a
/// hard-coded plan.
///
/// A planner is <b>stateless</b>: all context it needs (prior steps,
/// observations, the trigger for a re-plan) is passed in explicitly. This
/// lets the executor call the planner from arbitrary points in the loop
/// without having to reconstruct a running conversation.
/// </summary>
public interface IPlanner
{
    /// <summary>
    /// Produces the initial <see cref="RuntimePlan"/> for a goal. Called once
    /// at the start of an engine run.
    /// </summary>
    /// <param name="goal">Natural-language goal. Acceptance criteria, if any, should be embedded in the goal string — the planner does not own acceptance criteria, the mission layer does.</param>
    /// <param name="context">Optional prior context the planner should consider (memory excerpts, scratchpad entries from a prior run, etc). May be empty.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<RuntimePlan> PlanAsync(string goal, PlanContext context, CancellationToken ct = default);

    /// <summary>
    /// Produces a *patch* to an existing plan in response to a contradiction,
    /// tool failure, or scope reduction. The returned plan carries the same
    /// <see cref="RuntimePlan.Id"/> (family) with an incremented
    /// <see cref="RuntimePlan.PlanVersion"/>, and may reuse already-executed
    /// steps verbatim to avoid re-running them.
    /// </summary>
    /// <param name="currentPlan">The plan version that was executing when the re-plan was requested.</param>
    /// <param name="completedOutcomes">Step outcomes already recorded for this plan. The planner uses these to decide which steps stay and which need rewriting.</param>
    /// <param name="trigger">Why the re-plan was requested.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<RuntimePlan> ReplanAsync(
        RuntimePlan currentPlan,
        IReadOnlyList<StepOutcome> completedOutcomes,
        ReplanTrigger trigger,
        CancellationToken ct = default);
}

/// <summary>
/// Ambient context passed to the planner. Kept narrow on purpose — the
/// planner should not be able to reach into the runtime or trigger side
/// effects; it only reads these fields and returns a plan.
/// </summary>
/// <param name="SessionId">The session that owns this engine run. Used for logging and trace tagging only.</param>
/// <param name="MemoryExcerpts">Optional excerpts from the Phase 27 memory system that the caller considered relevant to the goal.</param>
/// <param name="ScratchpadEntries">Optional prior entries from the mission scratchpad (Phase 51 step D) — used when the planner is replanning across sub-agent waves.</param>
public sealed record PlanContext(
    string SessionId,
    IReadOnlyList<string> MemoryExcerpts,
    IReadOnlyList<string> ScratchpadEntries)
{
    /// <summary>Empty context — no memory, no scratchpad. Convenience for the first plan of a fresh run.</summary>
    public static PlanContext Empty(string sessionId) =>
        new(sessionId, Array.Empty<string>(), Array.Empty<string>());
}

/// <summary>
/// Why the executor asked for a re-plan. The planner uses this to decide
/// between "patch the tail of the plan" and "rewrite from the current step".
/// </summary>
/// <param name="Kind">Categorical reason — drives default re-plan strategy.</param>
/// <param name="Detail">Human-readable detail, persisted verbatim to the trace so a re-plan decision is inspectable after the fact.</param>
/// <param name="OffendingStepIndex">Index of the step that prompted the re-plan, or <c>null</c> if the trigger is plan-wide (e.g. budget cut).</param>
public sealed record ReplanTrigger(
    ReplanReason Kind,
    string Detail,
    int? OffendingStepIndex);

/// <summary>Enumerated reasons a re-plan can be requested.</summary>
public enum ReplanReason
{
    /// <summary>A step's actual outcome contradicted its <see cref="RuntimeStep.ExpectedOutcome"/>.</summary>
    Contradiction,

    /// <summary>A step failed twice in a row (or at the configured retry threshold).</summary>
    RepeatedFailure,

    /// <summary>The mission guard asked for scope reduction — the planner must drop low-priority steps.</summary>
    ScopeReduction,

    /// <summary>A human injected a directive mid-run; the planner must incorporate it.</summary>
    HumanDirective,

    /// <summary>A parallel sub-agent wrote a scratchpad entry that invalidates a later step's assumptions.</summary>
    ScratchpadInvalidation,
}
