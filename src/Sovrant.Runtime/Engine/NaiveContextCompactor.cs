using System.Globalization;
using System.Text;

namespace Sovrant.Runtime.Engine;

/// <summary>
/// Default <see cref="IContextCompactor"/>: deterministic, doesn't call
/// an LLM, good enough for tests and for the bare CLI runtime where the
/// planner budget is generous. Strategy:
///
/// 1. If the full outcome list already fits within <c>budget</c> characters
///    (measured as the total length of each outcome's <see cref="StepOutcome.Summary"/>
///    plus status/error text), return it unchanged.
/// 2. Otherwise keep the most recent outcomes verbatim up to
///    <see cref="MinKeepRecent"/> and fold everything older into a
///    summary line per bucket:
///    <c>step N (Succeeded): summary | step N (Failed): errorMessage</c>.
///    The summary is trimmed from the front if it still exceeds the
///    budget, on the theory that the newest folded events are usually
///    more relevant to the next re-plan decision.
///
/// This compactor is intentionally lossy but predictable — a test can
/// compute the exact output for a given input, which lets re-plan loop
/// tests assert on planner inputs without mocking the compactor.
/// </summary>
public sealed class NaiveContextCompactor : IContextCompactor
{
    /// <summary>Minimum number of recent outcomes to preserve verbatim even when the budget is very tight.</summary>
    public int MinKeepRecent { get; init; } = 3;

    public Task<CompactedContext> CompactAsync(
        IReadOnlyList<StepOutcome> outcomes,
        int budget,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budget);

        var totalCost = 0;
        foreach (var o in outcomes)
            totalCost += EstimateCost(o);

        if (totalCost <= budget)
        {
            // Nothing to do — the caller can feed the full list to the planner.
            return Task.FromResult(new CompactedContext(
                Summary: string.Empty,
                PreservedOutcomes: outcomes,
                FoldedCount: 0));
        }

        // Work out how many recent outcomes we can afford to keep verbatim.
        // Start from the newest and walk backwards until the running cost
        // would exceed budget minus a small summary reserve.
        var summaryReserve = Math.Min(budget / 4, 512);
        var keepBudget = budget - summaryReserve;
        var keepCount = 0;
        var keepCost = 0;
        for (var i = outcomes.Count - 1; i >= 0; i--)
        {
            var cost = EstimateCost(outcomes[i]);
            if (keepCount >= MinKeepRecent && keepCost + cost > keepBudget)
                break;
            keepCost += cost;
            keepCount++;
        }
        if (keepCount > outcomes.Count) keepCount = outcomes.Count;
        if (keepCount < MinKeepRecent) keepCount = Math.Min(MinKeepRecent, outcomes.Count);

        var foldedCount = outcomes.Count - keepCount;
        var preserved = new List<StepOutcome>(keepCount);
        for (var i = outcomes.Count - keepCount; i < outcomes.Count; i++)
            preserved.Add(outcomes[i]);

        var summary = BuildSummary(outcomes, foldedCount, summaryReserve);

        return Task.FromResult(new CompactedContext(
            Summary: summary,
            PreservedOutcomes: preserved,
            FoldedCount: foldedCount));
    }

    private static int EstimateCost(StepOutcome outcome)
    {
        // Rough proxy for how much space this outcome will take when
        // rendered into the planner prompt.
        var len = outcome.Summary?.Length ?? 0;
        if (outcome.ErrorMessage is { Length: > 0 } err) len += err.Length;
        if (outcome.ObservationJson is { Length: > 0 } obs) len += Math.Min(obs.Length, 512);
        return len + 32; // header overhead (status + step index + attempt marker)
    }

    private static string BuildSummary(
        IReadOnlyList<StepOutcome> outcomes, int foldedCount, int summaryBudget)
    {
        if (foldedCount <= 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append(
            CultureInfo.InvariantCulture,
            $"Compacted {foldedCount} earlier step outcome(s): ");

        for (var i = 0; i < foldedCount; i++)
        {
            var o = outcomes[i];
            var detail = o.Status == StepStatus.Failed
                ? o.ErrorMessage ?? o.Summary ?? "(no detail)"
                : o.Summary ?? "(no summary)";
            var fragment = string.Create(
                CultureInfo.InvariantCulture,
                $"[step {o.StepIndex} {o.Status}: {Truncate(detail, 80)}]");
            if (sb.Length + fragment.Length > summaryBudget)
            {
                sb.Append("...");
                break;
            }
            if (i > 0) sb.Append(' ');
            sb.Append(fragment);
        }

        return sb.ToString();
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }
}
