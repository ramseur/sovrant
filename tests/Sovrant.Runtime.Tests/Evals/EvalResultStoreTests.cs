using Sovrant.Runtime.Evals;

namespace Sovrant.Runtime.Tests.Evals;

public sealed class EvalResultStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-eval-store-{Guid.NewGuid():N}");
    private readonly EvalResultStore _store;

    public EvalResultStoreTests()
    {
        _store = new EvalResultStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectory_And_WritesFile()
    {
        var report = CreateReport("test-suite");
        await _store.SaveAsync(report);

        Assert.True(Directory.Exists(_tempDir));
        var files = Directory.GetFiles(_tempDir, "*.json");
        Assert.Single(files);
        Assert.Contains("test-suite_", Path.GetFileName(files[0]));
    }

    [Fact]
    public async Task SaveAsync_FileContainsExpectedFields()
    {
        var report = CreateReport("my-suite");
        await _store.SaveAsync(report);

        var files = Directory.GetFiles(_tempDir, "*.json");
        var json = await File.ReadAllTextAsync(files[0]);

        Assert.Contains("my-suite", json);
        Assert.Contains("pass_rate", json);
        Assert.Contains("pass_at_1_rate", json);
        Assert.Contains("results", json);
    }

    [Fact]
    public async Task LoadHistory_ReturnsEmptyWhenNoResults()
    {
        var history = _store.LoadHistory("nonexistent");
        Assert.Empty(history);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task LoadHistory_ReturnsPersistedResults()
    {
        var report = CreateReport("trend-suite");
        await _store.SaveAsync(report);

        var history = _store.LoadHistory("trend-suite");

        Assert.Single(history);
        Assert.Equal("trend-suite", history[0].SuiteName);
    }

    [Fact]
    public async Task LoadHistory_MultipleResults_OrderedByMostRecent()
    {
        // Save two reports with slightly different timestamps
        var report1 = new EvalReport
        {
            SuiteName = "ordered",
            Results = [],
            StartedAt = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromSeconds(1),
        };
        var report2 = new EvalReport
        {
            SuiteName = "ordered",
            Results = [],
            StartedAt = new DateTimeOffset(2025, 1, 1, 11, 0, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromSeconds(2),
        };

        await _store.SaveAsync(report1);
        await _store.SaveAsync(report2);

        var history = _store.LoadHistory("ordered");

        Assert.Equal(2, history.Count);
        // Most recent first (file names sorted descending)
        Assert.True(history[0].StartedAt >= history[1].StartedAt);
    }

    [Fact]
    public async Task LoadHistory_RespectsMaxResults()
    {
        for (int i = 0; i < 5; i++)
        {
            var report = new EvalReport
            {
                SuiteName = "limited",
                Results = [],
                StartedAt = new DateTimeOffset(2025, 1, 1, i, 0, 0, TimeSpan.Zero),
                Duration = TimeSpan.FromSeconds(1),
            };
            await _store.SaveAsync(report);
        }

        var history = _store.LoadHistory("limited", maxResults: 3);
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public async Task LoadHistory_IgnoresOtherSuites()
    {
        await _store.SaveAsync(CreateReport("suite-a"));
        await _store.SaveAsync(CreateReport("suite-b"));

        var history = _store.LoadHistory("suite-a");
        Assert.Single(history);
        Assert.Equal("suite-a", history[0].SuiteName);
    }

    private static EvalReport CreateReport(string suiteName) => new()
    {
        SuiteName = suiteName,
        Results = [
            new EvalResult
            {
                EvalName = "eval-1",
                Attempts = [new GradeResult(true, "ok")],
                Passed = true,
                GraderType = GraderType.Code,
            },
        ],
        StartedAt = DateTimeOffset.UtcNow,
        Duration = TimeSpan.FromSeconds(1),
    };
}
