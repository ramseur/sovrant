using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — LLM-backed <see cref="IContextCompactor"/>. When prior
/// step outcomes exceed the budget, asks an LLM (via the session pool)
/// to produce a concise summary of the folded outcomes. Falls back to
/// <see cref="NaiveContextCompactor"/> if the LLM call fails so the
/// engine never stalls on a compaction error.
///
/// The LLM produces higher-quality summaries than the naive compactor
/// because it can identify the *salient* outcomes (the ones most likely
/// to affect the next re-plan) rather than just listing statuses in
/// chronological order.
/// </summary>
public sealed partial class LlmContextCompactor : IContextCompactor
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "LlmContextCompactor: LLM summarization failed, falling back to naive compactor: {Error}")]
    private static partial void LogFallback(ILogger logger, string error);

    private readonly IRuntimeSessionPool _sessionPool;
    private readonly NaiveContextCompactor _fallback = new();
    private readonly ILogger<LlmContextCompactor> _logger;

    /// <summary>Session id reserved for compaction requests so they don't pollute user sessions.</summary>
    private const string CompactorSessionId = "__sovrant_context_compactor__";

    public LlmContextCompactor(
        IRuntimeSessionPool sessionPool,
        ILogger<LlmContextCompactor>? logger = null)
    {
        _sessionPool = sessionPool ?? throw new ArgumentNullException(nameof(sessionPool));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LlmContextCompactor>.Instance;
    }

    public async Task<CompactedContext> CompactAsync(
        IReadOnlyList<StepOutcome> outcomes,
        int budget,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budget);

        // Quick check: if everything fits, return unchanged — no LLM call needed.
        var totalCost = 0;
        foreach (var o in outcomes)
            totalCost += EstimateCost(o);

        if (totalCost <= budget)
        {
            return new CompactedContext(
                Summary: string.Empty,
                PreservedOutcomes: outcomes,
                FoldedCount: 0);
        }

        // Decide which outcomes to preserve verbatim (recent) vs. fold (older).
        const int minKeepRecent = 3;
        var summaryReserve = Math.Min(budget / 4, 1024);
        var keepBudget = budget - summaryReserve;
        var keepCount = 0;
        var keepCost = 0;
        for (var i = outcomes.Count - 1; i >= 0; i--)
        {
            var cost = EstimateCost(outcomes[i]);
            if (keepCount >= minKeepRecent && keepCost + cost > keepBudget)
                break;
            keepCost += cost;
            keepCount++;
        }
        keepCount = Math.Clamp(keepCount, Math.Min(minKeepRecent, outcomes.Count), outcomes.Count);
        var foldedCount = outcomes.Count - keepCount;

        if (foldedCount == 0)
        {
            return new CompactedContext(
                Summary: string.Empty,
                PreservedOutcomes: outcomes,
                FoldedCount: 0);
        }

        var preserved = new List<StepOutcome>(keepCount);
        for (var i = outcomes.Count - keepCount; i < outcomes.Count; i++)
            preserved.Add(outcomes[i]);

        // Build a prompt that asks the LLM to summarize the folded outcomes.
        var prompt = BuildSummaryPrompt(outcomes, foldedCount, summaryReserve);

        try
        {
            var summary = await AskLlmAsync(prompt, ct).ConfigureAwait(false);

            // Truncate the summary to the budget if the LLM was verbose.
            if (summary.Length > summaryReserve)
                summary = string.Concat(summary.AsSpan(0, summaryReserve - 1), "…");

            return new CompactedContext(
                Summary: summary,
                PreservedOutcomes: preserved,
                FoldedCount: foldedCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFallback(_logger, ex.Message);
            return await _fallback.CompactAsync(outcomes, budget, ct).ConfigureAwait(false);
        }
    }

    private async Task<string> AskLlmAsync(string prompt, CancellationToken ct)
    {
        var pooled = await _sessionPool
            .GetOrCreateAsync(CompactorSessionId, scopedRouterOverride: null, ownerUserId: null, ct: ct)
            .ConfigureAwait(false);

        var text = new StringBuilder();
        await pooled.Lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await foreach (var evt in pooled.Runtime.RunTurnAsync(prompt, ct).ConfigureAwait(false))
            {
                if (evt is RuntimeEvent.TextChunk tc)
                    text.Append(tc.Text);
            }
        }
        finally
        {
            pooled.Lock.Release();
        }

        return text.ToString().Trim();
    }

    private static string BuildSummaryPrompt(
        IReadOnlyList<StepOutcome> outcomes, int foldedCount, int budgetHint)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Summarize the following step outcomes into a concise paragraph.");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Keep the summary under {budgetHint} characters.");
        sb.AppendLine("Focus on: what was attempted, what succeeded, what failed, and any key observations.");
        sb.AppendLine("Do NOT list every step individually — synthesize the information.");
        sb.AppendLine();

        for (var i = 0; i < foldedCount; i++)
        {
            var o = outcomes[i];
            sb.Append(CultureInfo.InvariantCulture,
                $"Step {o.StepIndex} [{o.Status}]: ");
            if (!string.IsNullOrEmpty(o.Summary))
                sb.Append(Truncate(o.Summary, 200));
            if (o.Status == StepStatus.Failed && !string.IsNullOrEmpty(o.ErrorMessage))
                sb.Append(CultureInfo.InvariantCulture, $" (error: {Truncate(o.ErrorMessage, 100)})");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static int EstimateCost(StepOutcome outcome)
    {
        var len = outcome.Summary?.Length ?? 0;
        if (outcome.ErrorMessage is { Length: > 0 } err) len += err.Length;
        if (outcome.ObservationJson is { Length: > 0 } obs) len += Math.Min(obs.Length, 512);
        return len + 32;
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");
    }
}
