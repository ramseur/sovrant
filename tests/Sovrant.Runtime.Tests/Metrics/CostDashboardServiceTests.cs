using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Metrics;

namespace Sovrant.Runtime.Tests.Metrics;

public sealed class CostDashboardServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CostMetricsLogger _metricsLogger;

    public CostDashboardServiceTests()
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
    public void GetSummary_NoRecords_ReturnsZeros()
    {
        var dashboard = new CostDashboardService(_metricsLogger);

        var summary = dashboard.GetSummary(CostRange.All);

        Assert.Equal(0, summary.TurnCount);
        Assert.Equal(0m, summary.TotalEstimatedUsd);
        Assert.Empty(summary.BySessions);
        Assert.Empty(summary.ByModels);
    }

    [Fact]
    public void GetSummary_AggregatesBySessionAndModel()
    {
        SeedRecords(
            ("s1", "gpt-4o", 1000, 500, 0.01m),
            ("s1", "gpt-4o", 2000, 800, 0.02m),
            ("s2", "claude-sonnet", 500, 200, 0.005m));

        var dashboard = new CostDashboardService(_metricsLogger);
        var summary = dashboard.GetSummary(CostRange.All);

        Assert.Equal(3, summary.TurnCount);
        Assert.Equal(0.035m, summary.TotalEstimatedUsd);
        Assert.Equal(2, summary.BySessions.Count);
        Assert.Equal(2, summary.ByModels.Count);

        var s1 = summary.BySessions.First(s => s.SessionId == "s1");
        Assert.Equal(0.03m, s1.EstimatedUsd);
        Assert.Equal(2, s1.TurnCount);
    }

    [Fact]
    public void GetSummary_Daily_FiltersOldRecords()
    {
        // Write a record with a recent timestamp
        _metricsLogger.Append(new CostTurnRecord
        {
            SessionId = "s1",
            Model = "gpt-4o",
            InputTokens = 100,
            OutputTokens = 50,
            EstimatedUsd = 0.001m,
            Source = "test",
            Timestamp = DateTimeOffset.UtcNow
        });

        var dashboard = new CostDashboardService(_metricsLogger);
        var summary = dashboard.GetSummary(CostRange.Daily);

        Assert.Equal(1, summary.TurnCount);
    }

    [Fact]
    public void GetSummary_IncludesBudgetInfo_WhenEnforcerPresent()
    {
        var enforcer = new BudgetEnforcer(
            NullLogger<BudgetEnforcer>.Instance,
            sessionBudgetUsd: 5.00m,
            projectBudgetUsd: 50.00m);

        var dashboard = new CostDashboardService(_metricsLogger, enforcer);
        var summary = dashboard.GetSummary(CostRange.All);

        Assert.Equal(5.00m, summary.SessionBudgetUsd);
        Assert.Equal(50.00m, summary.ProjectBudgetUsd);
    }

    private void SeedRecords(params (string SessionId, string Model, long Input, long Output, decimal Cost)[] records)
    {
        foreach (var (sessionId, model, input, output, cost) in records)
        {
            _metricsLogger.Append(new CostTurnRecord
            {
                SessionId = sessionId,
                Model = model,
                InputTokens = input,
                OutputTokens = output,
                EstimatedUsd = cost,
                Source = "test",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}
