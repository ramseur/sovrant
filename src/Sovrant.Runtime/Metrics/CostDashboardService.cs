namespace Sovrant.Runtime.Metrics;

/// <summary>
/// Aggregation service over the JSONL cost metrics log.
/// Produces daily/weekly/monthly summaries with per-session and per-model breakdowns.
/// </summary>
public sealed class CostDashboardService
{
    private readonly CostMetricsLogger _metricsLogger;
    private readonly BudgetEnforcer? _budgetEnforcer;

    public CostDashboardService(CostMetricsLogger metricsLogger, BudgetEnforcer? budgetEnforcer = null)
    {
        _metricsLogger = metricsLogger;
        _budgetEnforcer = budgetEnforcer;
    }

    /// <summary>
    /// Returns an aggregated cost summary for the given time range.
    /// </summary>
    public CostSummary GetSummary(CostRange range = CostRange.Daily)
    {
        var records = _metricsLogger.ReadAll();
        var cutoff = range switch
        {
            CostRange.Daily => DateTimeOffset.UtcNow.AddDays(-1),
            CostRange.Weekly => DateTimeOffset.UtcNow.AddDays(-7),
            CostRange.Monthly => DateTimeOffset.UtcNow.AddDays(-30),
            CostRange.All => DateTimeOffset.MinValue,
            _ => DateTimeOffset.UtcNow.AddDays(-1)
        };

        var filtered = records.Where(r => r.Timestamp >= cutoff).ToList();

        var totalEstimatedUsd = filtered
            .Where(r => r.EstimatedUsd.HasValue)
            .Sum(r => r.EstimatedUsd!.Value);

        var totalActualUsd = filtered
            .Where(r => r.ActualUsd.HasValue)
            .Sum(r => r.ActualUsd!.Value);

        var totalInputTokens = filtered.Sum(r => r.InputTokens);
        var totalOutputTokens = filtered.Sum(r => r.OutputTokens);

        var bySession = filtered
            .GroupBy(r => r.SessionId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SessionCostSummary(
                g.Key,
                g.Sum(r => r.EstimatedUsd ?? 0),
                g.Sum(r => r.InputTokens),
                g.Sum(r => r.OutputTokens),
                g.Count()))
            .OrderByDescending(s => s.EstimatedUsd)
            .ToList();

        var byModel = filtered
            .GroupBy(r => r.Model, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ModelCostSummary(
                g.Key,
                g.Sum(r => r.EstimatedUsd ?? 0),
                g.Sum(r => r.InputTokens),
                g.Sum(r => r.OutputTokens),
                g.Count()))
            .OrderByDescending(s => s.EstimatedUsd)
            .ToList();

        return new CostSummary
        {
            Range = range,
            TotalEstimatedUsd = totalEstimatedUsd,
            TotalActualUsd = totalActualUsd > 0 ? totalActualUsd : null,
            TotalInputTokens = totalInputTokens,
            TotalOutputTokens = totalOutputTokens,
            TurnCount = filtered.Count,
            SessionBudgetUsd = _budgetEnforcer?.SessionBudgetUsd,
            ProjectBudgetUsd = _budgetEnforcer?.ProjectBudgetUsd,
            ProjectSpendUsd = _budgetEnforcer?.ProjectSpend,
            BySessions = bySession,
            ByModels = byModel
        };
    }
}

/// <summary>Time range for cost aggregation.</summary>
public enum CostRange
{
    Daily,
    Weekly,
    Monthly,
    All
}

/// <summary>Aggregated cost summary.</summary>
public sealed record CostSummary
{
    public required CostRange Range { get; init; }
    public required decimal TotalEstimatedUsd { get; init; }
    public decimal? TotalActualUsd { get; init; }
    public required long TotalInputTokens { get; init; }
    public required long TotalOutputTokens { get; init; }
    public required int TurnCount { get; init; }
    public decimal? SessionBudgetUsd { get; init; }
    public decimal? ProjectBudgetUsd { get; init; }
    public decimal? ProjectSpendUsd { get; init; }
    public required IReadOnlyList<SessionCostSummary> BySessions { get; init; }
    public required IReadOnlyList<ModelCostSummary> ByModels { get; init; }
}

/// <summary>Cost breakdown for a single session.</summary>
public sealed record SessionCostSummary(
    string SessionId,
    decimal EstimatedUsd,
    long InputTokens,
    long OutputTokens,
    int TurnCount);

/// <summary>Cost breakdown for a single model.</summary>
public sealed record ModelCostSummary(
    string Model,
    decimal EstimatedUsd,
    long InputTokens,
    long OutputTokens,
    int TurnCount);
