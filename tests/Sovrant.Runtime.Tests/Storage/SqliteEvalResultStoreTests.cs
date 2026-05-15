using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Evals;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteEvalResultStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteEvalResultStore _store;

    public SqliteEvalResultStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_eval_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteEvalResultStore((ISqliteConnectionFactory)_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private static EvalReport CreateReport(string suite = "test-suite", int passCount = 2, int failCount = 1)
    {
        var results = new List<EvalResult>();
        for (var i = 0; i < passCount; i++)
        {
            results.Add(new EvalResult
            {
                EvalName = $"pass-eval-{i}",
                Category = EvalCategory.Capability,
                GraderType = GraderType.Code,
                Passed = true,
                Attempts = [new GradeResult(true, "ok", 1.0, TimeSpan.FromSeconds(1))],
            });
        }
        for (var i = 0; i < failCount; i++)
        {
            results.Add(new EvalResult
            {
                EvalName = $"fail-eval-{i}",
                Category = EvalCategory.Quality,
                GraderType = GraderType.Model,
                Passed = false,
                Attempts = [new GradeResult(false, "wrong", 0.2, TimeSpan.FromSeconds(2))],
            });
        }

        return new EvalReport
        {
            SuiteName = suite,
            StartedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromSeconds(10),
            Results = results,
        };
    }

    [Fact]
    public async Task SaveAndLoadHistory_RoundTrips()
    {
        var report = CreateReport();
        await _store.SaveAsync(report);

        var history = _store.LoadHistory("test-suite");

        Assert.Single(history);
        var summary = history[0];
        Assert.Equal("test-suite", summary.SuiteName);
        Assert.Equal(2, summary.TotalPassed);
        Assert.Equal(1, summary.TotalFailed);
        Assert.Equal(0, summary.TotalSkipped);
    }

    [Fact]
    public async Task LoadHistory_ReturnsEmpty_WhenNoData()
    {
        var history = _store.LoadHistory("nonexistent");
        Assert.Empty(history);
    }

    [Fact]
    public async Task LoadHistory_OrdersByMostRecent()
    {
        // Save two reports with different timestamps
        var report1 = new EvalReport
        {
            SuiteName = "ordering",
            StartedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromSeconds(5),
            Results = [],
        };
        var report2 = new EvalReport
        {
            SuiteName = "ordering",
            StartedAt = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromSeconds(5),
            Results = [],
        };

        await _store.SaveAsync(report1);
        await _store.SaveAsync(report2);

        var history = _store.LoadHistory("ordering");

        Assert.Equal(2, history.Count);
        // Most recent first
        Assert.True(history[0].StartedAt > history[1].StartedAt);
    }

    [Fact]
    public async Task LoadHistory_RespectsMaxResults()
    {
        for (var i = 0; i < 5; i++)
            await _store.SaveAsync(CreateReport());

        var history = _store.LoadHistory("test-suite", maxResults: 3);
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public async Task LoadHistory_FiltersBySuiteName()
    {
        await _store.SaveAsync(CreateReport("suite-a"));
        await _store.SaveAsync(CreateReport("suite-b"));

        var historyA = _store.LoadHistory("suite-a");
        var historyB = _store.LoadHistory("suite-b");

        Assert.Single(historyA);
        Assert.Single(historyB);
        Assert.Equal("suite-a", historyA[0].SuiteName);
        Assert.Equal("suite-b", historyB[0].SuiteName);
    }

    [Fact]
    public async Task Save_PreservesPassRates()
    {
        var report = CreateReport(passCount: 3, failCount: 1);
        await _store.SaveAsync(report);

        var history = _store.LoadHistory("test-suite");
        var summary = Assert.Single(history);

        Assert.Equal(0.75, summary.PassRate, 2);
        Assert.Equal(3, summary.TotalPassed);
        Assert.Equal(1, summary.TotalFailed);
    }
}
