namespace Sovrant.Runtime.Metrics;

/// <summary>
/// Combined facade over <see cref="ICostModel"/> + <see cref="CostMetricsLogger"/>
/// so the runtime calls one method (<see cref="RecordTurnAsync"/>) and both the cost
/// estimate and the JSONL write happen atomically.
/// </summary>
public sealed class CostModelLoggerFacade
{
    private readonly ICostModel _costModel;
    private readonly CostMetricsLogger _metricsLogger;
    private readonly BudgetEnforcer? _budgetEnforcer;

    public CostModelLoggerFacade(
        ICostModel costModel,
        CostMetricsLogger metricsLogger,
        BudgetEnforcer? budgetEnforcer = null)
    {
        _costModel = costModel;
        _metricsLogger = metricsLogger;
        _budgetEnforcer = budgetEnforcer;
    }

    /// <summary>
    /// Estimates cost, writes a JSONL record, and optionally enforces budget.
    /// Returns the cost turn record that was logged.
    /// </summary>
    public CostTurnRecord RecordTurn(
        string sessionId,
        string model,
        long inputTokens,
        long outputTokens,
        CostHints? hints = null,
        string? generationId = null)
    {
        var estimatedUsd = _costModel.EstimateCost(model, inputTokens, outputTokens, hints);

        var record = new CostTurnRecord
        {
            SessionId = sessionId,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedUsd = estimatedUsd,
            ActualUsd = null,
            Source = estimatedUsd is not null ? "openrouter" : "none",
            Timestamp = DateTimeOffset.UtcNow,
            GenerationId = generationId
        };

        _metricsLogger.Append(record);

        if (estimatedUsd is not null)
            _budgetEnforcer?.RecordSpend(sessionId, estimatedUsd.Value);

        return record;
    }
}
