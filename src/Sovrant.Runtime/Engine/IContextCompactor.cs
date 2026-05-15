namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — seam for shrinking a run's accumulated step history when it
/// grows past the planner's (or a sub-agent's) context budget. Long runs
/// with a lot of re-plans can accumulate dozens of <see cref="StepOutcome"/>
/// rows; the planner can't usefully look at all of them when it decides
/// how to patch the next plan version, so the executor asks an
/// <see cref="IContextCompactor"/> to fold older outcomes into a single
/// summary entry while preserving the most recent N verbatim.
///
/// Why a seam rather than an inline helper: the "good" compactor talks
/// to an LLM ("summarise what happened in steps 0..17 and what the
/// planner should remember") while the default compactor is a cheap
/// deterministic truncator. Tests drive the re-plan loop against the
/// deterministic version; production swaps in the LLM-backed one at
/// composition time.
///
/// Compaction is read-only with respect to the trace log: the durable
/// <c>runtime_traces</c> rows are never mutated. Only the in-memory
/// view the planner receives is compacted.
/// </summary>
public interface IContextCompactor
{
    /// <summary>
    /// Returns a compacted view of <paramref name="outcomes"/> suitable
    /// for handing to a planner or sub-agent. The implementation decides
    /// whether to compact at all (short lists should be returned
    /// unchanged) and how aggressively to fold older entries.
    /// </summary>
    /// <param name="outcomes">Full outcome history, oldest first, across every plan version that has run so far.</param>
    /// <param name="budget">Soft budget the caller wants the compacted view to fit inside. Unit is implementation-defined (character count for the default compactor, token count for the LLM version).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CompactedContext> CompactAsync(
        IReadOnlyList<StepOutcome> outcomes,
        int budget,
        CancellationToken ct = default);
}

/// <summary>
/// Output of <see cref="IContextCompactor.CompactAsync"/>. Contains a
/// prose summary of the outcomes that were folded away plus a tail of
/// recent outcomes that were kept verbatim.
/// </summary>
/// <param name="Summary">Short prose describing the folded portion. Empty string when no compaction was needed.</param>
/// <param name="PreservedOutcomes">Most recent outcomes kept verbatim. Always a suffix of the input list, in the same order. May be the entire input list if no compaction was needed.</param>
/// <param name="FoldedCount">Number of outcomes from the original list that were folded into <see cref="Summary"/>. Zero when no compaction was needed; equals <c>outcomes.Count - PreservedOutcomes.Count</c>.</param>
public sealed record CompactedContext(
    string Summary,
    IReadOnlyList<StepOutcome> PreservedOutcomes,
    int FoldedCount);
