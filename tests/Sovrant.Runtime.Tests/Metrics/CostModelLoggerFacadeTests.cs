using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Metrics;

namespace Sovrant.Runtime.Tests.Metrics;

public sealed class CostModelLoggerFacadeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CostMetricsLogger _metricsLogger;

    public CostModelLoggerFacadeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _metricsLogger = new CostMetricsLogger(
            NullLogger<CostMetricsLogger>.Instance,
            Path.Combine(_tempDir, "cost.jsonl"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void RecordTurn_WithKnownModel_ReturnsCostAndLogs()
    {
        var costModel = new FakeCostModel(0.0105m);
        var facade = new CostModelLoggerFacade(costModel, _metricsLogger);

        var record = facade.RecordTurn("session-1", "gpt-4o", 1000, 500);

        Assert.Equal(0.0105m, record.EstimatedUsd);
        Assert.Equal("openrouter", record.Source);
        Assert.Equal("session-1", record.SessionId);
        Assert.Equal(1000, record.InputTokens);
        Assert.Equal(500, record.OutputTokens);

        var persisted = _metricsLogger.ReadAll();
        Assert.Single(persisted);
    }

    [Fact]
    public void RecordTurn_UnknownModel_ReturnsNullCostAndSourceNone()
    {
        var costModel = new FakeCostModel(null);
        var facade = new CostModelLoggerFacade(costModel, _metricsLogger);

        var record = facade.RecordTurn("session-1", "unknown-model", 100, 50);

        Assert.Null(record.EstimatedUsd);
        Assert.Equal("none", record.Source);
    }

    [Fact]
    public void RecordTurn_WithBudgetEnforcer_RecordsSpend()
    {
        var costModel = new FakeCostModel(1.50m);
        var enforcer = new BudgetEnforcer(
            NullLogger<BudgetEnforcer>.Instance,
            sessionBudgetUsd: 5.00m);
        var facade = new CostModelLoggerFacade(costModel, _metricsLogger, enforcer);

        facade.RecordTurn("s1", "gpt-4o", 1000, 500);
        facade.RecordTurn("s1", "gpt-4o", 1000, 500);

        Assert.Equal(3.00m, enforcer.GetSessionSpend("s1"));
    }

    private sealed class FakeCostModel(decimal? fixedCost) : ICostModel
    {
        public decimal? EstimateCost(string model, long inputTokens, long outputTokens, CostHints? hints = null)
            => fixedCost;
    }
}
