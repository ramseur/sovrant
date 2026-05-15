using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteMemoryStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IMemoryStore _store;

    public SqliteMemoryStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMemoryStore((ISqliteConnectionFactory)_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── Summaries ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadSummary_RoundTrips()
    {
        var summary = new SessionSummary
        {
            SessionId = "s1",
            Project = "/proj",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAt = DateTimeOffset.UtcNow,
            Tasks = ["build feature"],
            ToolsUsed = ["Bash", "Edit"],
            FilesModified = ["src/foo.cs"],
            Outcome = SessionOutcome.Success,
            TotalInputTokens = 100,
            TotalOutputTokens = 200,
            TurnCount = 3,
            ErrorCount = 0,
        };
        await _store.SaveSummaryAsync(summary);

        var loaded = await _store.LoadSummariesAsync("/proj");

        Assert.Single(loaded);
        Assert.Equal("s1", loaded[0].SessionId);
        Assert.Equal(["build feature"], loaded[0].Tasks);
        Assert.Equal(SessionOutcome.Success, loaded[0].Outcome);
    }

    [Fact]
    public async Task LoadSummaries_RespectsMaxCount()
    {
        for (var i = 0; i < 10; i++)
        {
            await _store.SaveSummaryAsync(new SessionSummary
            {
                SessionId = $"s{i}",
                Project = "/proj",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10 + i),
                EndedAt = DateTimeOffset.UtcNow.AddMinutes(-9 + i),
            });
        }

        var loaded = await _store.LoadSummariesAsync("/proj", maxCount: 3);
        Assert.Equal(3, loaded.Count);
    }

    // ── Patterns ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadPattern_RoundTrips()
    {
        var pattern = new LearnedPattern
        {
            Id = "p1",
            Pattern = "Use xUnit",
            Project = "/proj",
            Confidence = 0.8,
        };
        await _store.SavePatternAsync(pattern);

        var loaded = await _store.LoadPatternsAsync("/proj");

        Assert.Single(loaded);
        Assert.Equal("Use xUnit", loaded[0].Pattern);
        Assert.Equal(0.8, loaded[0].Confidence);
    }

    [Fact]
    public async Task RemovePattern_Removes()
    {
        await _store.SavePatternAsync(new LearnedPattern { Id = "p1", Pattern = "test", Project = "/proj" });
        await _store.RemovePatternAsync("p1", "/proj");

        var loaded = await _store.LoadPatternsAsync("/proj");
        Assert.Empty(loaded);
    }

    // ── Instincts ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadInstinct_RoundTrips()
    {
        var instinct = new Instinct
        {
            Id = "i1",
            Trigger = "user says deploy",
            Action = "run tests first",
            Confidence = 0.7,
            Evidence = ["worked last time"],
        };
        await _store.SaveInstinctAsync(instinct);

        var loaded = await _store.LoadInstinctsAsync();

        Assert.Single(loaded);
        Assert.Equal("run tests first", loaded[0].Action);
        Assert.Equal(["worked last time"], loaded[0].Evidence);
    }

    [Fact]
    public async Task ReinforceInstinct_IncreasesConfidence()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "t", Action = "a", Confidence = 0.5 });
        await _store.ReinforceInstinctAsync("i1", "positive signal");

        var loaded = await _store.LoadInstinctsAsync();
        Assert.Equal(0.6, loaded[0].Confidence, precision: 2);
    }

    [Fact]
    public async Task CorrectInstinct_DecreasesConfidenceAndMayPrune()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "t", Action = "a", Confidence = 0.35 });
        await _store.CorrectInstinctAsync("i1", "negative signal");

        // Confidence 0.35 - 0.15 = 0.20 < 0.3 threshold → pruned
        var loaded = await _store.LoadInstinctsAsync();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task PruneInstincts_RemovesBelowThreshold()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "t", Action = "a", Confidence = 0.2 });
        await _store.SaveInstinctAsync(new Instinct { Id = "i2", Trigger = "t", Action = "a", Confidence = 0.8 });

        var pruned = await _store.PruneInstinctsAsync();

        Assert.Equal(1, pruned);
        var remaining = await _store.LoadInstinctsAsync();
        Assert.Single(remaining);
        Assert.Equal("i2", remaining[0].Id);
    }

    [Fact]
    public async Task RemoveInstinct_Removes()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "t", Action = "a" });
        await _store.RemoveInstinctAsync("i1");

        var loaded = await _store.LoadInstinctsAsync();
        Assert.Empty(loaded);
    }
}
