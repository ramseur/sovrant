namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — the result of executing a single <see cref="RuntimeStep"/>.
/// Distinct from a tool result: a step may expand into multiple tool calls
/// (via a macro) and the outcome summarises the whole step's effect plus
/// whether it matched the planner's <see cref="RuntimeStep.ExpectedOutcome"/>.
///
/// This is the type the executor writes to <c>runtime_traces</c> alongside
/// the underlying tool calls, and the type the planner inspects when the
/// executor requests a re-plan.
/// </summary>
/// <param name="StepIndex">Index of the <see cref="RuntimeStep"/> within its parent plan.</param>
/// <param name="Status">High-level status — did the step succeed, fail, or produce output that contradicts the plan.</param>
/// <param name="Summary">Short natural-language summary of what actually happened. Durable; survives context compaction.</param>
/// <param name="ObservationJson">Structured observation payload (JSON). Optional; <c>null</c> when the step had no structured output.</param>
/// <param name="MatchedExpectation">True if the actual outcome matched the planner's <c>ExpectedOutcome</c>. <c>false</c> is a cheap re-plan trigger.</param>
/// <param name="StartedAt">When the executor started the step.</param>
/// <param name="CompletedAt">When the executor finished the step (success or failure).</param>
/// <param name="ErrorMessage">Populated when <see cref="Status"/> is <see cref="StepStatus.Failed"/>.</param>
public sealed record StepOutcome(
    int StepIndex,
    StepStatus Status,
    string Summary,
    string? ObservationJson,
    bool MatchedExpectation,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string? ErrorMessage = null);

/// <summary>
/// Outcome status for a single <see cref="RuntimeStep"/>. Distinct from
/// tool-level success: a step can fail even if the tool call succeeded
/// (e.g. the planner expected the file to contain X but it contained Y).
/// </summary>
public enum StepStatus
{
    /// <summary>Step ran cleanly and its output matched the planner's expectation.</summary>
    Succeeded,

    /// <summary>Step ran but its output contradicts the planner's expectation. Cheap re-plan trigger.</summary>
    Contradicted,

    /// <summary>Step failed outright (tool error, unrecoverable exception). May be retried or re-planned.</summary>
    Failed,

    /// <summary>Step was skipped because the executor reduced scope mid-plan (priority guard dropped it).</summary>
    Skipped,
}
