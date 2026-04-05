using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Memory;

namespace Sovrant.Runtime.Tests.Memory;

public sealed class FileMemoryStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileMemoryStore _store;

    public FileMemoryStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _store = new FileMemoryStore(
            Path.Combine(_tempDir, "summaries"),
            Path.Combine(_tempDir, "learned"),
            Path.Combine(_tempDir, "instincts"),
            NullLogger<FileMemoryStore>.Instance);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* best-effort cleanup */ }
    }

    // ── Session Summaries ───────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadSummary_RoundTrips()
    {
        var summary = new SessionSummary
        {
            SessionId = "sess-1",
            Project = "/my/project",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAt = DateTimeOffset.UtcNow,
            Tasks = ["Fix the bug"],
            ToolsUsed = ["bash", "edit_file"],
            Outcome = SessionOutcome.Success,
            TurnCount = 3,
            TotalInputTokens = 500,
            TotalOutputTokens = 200,
        };

        await _store.SaveSummaryAsync(summary);
        var loaded = await _store.LoadSummariesAsync("/my/project");

        Assert.Single(loaded);
        Assert.Equal("sess-1", loaded[0].SessionId);
        Assert.Equal(SessionOutcome.Success, loaded[0].Outcome);
        Assert.Equal(500, loaded[0].TotalInputTokens);
    }

    [Fact]
    public async Task LoadSummaries_RespectsMaxCount()
    {
        for (var i = 0; i < 5; i++)
        {
            await _store.SaveSummaryAsync(new SessionSummary
            {
                SessionId = $"sess-{i}",
                Project = "/proj",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10 + i),
                EndedAt = DateTimeOffset.UtcNow.AddMinutes(-9 + i),
            });
            // Small delay to ensure distinct timestamps in filenames
            await Task.Delay(10);
        }

        var loaded = await _store.LoadSummariesAsync("/proj", maxCount: 2);

        Assert.Equal(2, loaded.Count);
    }

    [Fact]
    public async Task LoadSummaries_NonexistentProject_ReturnsEmpty()
    {
        var loaded = await _store.LoadSummariesAsync("/does/not/exist");
        Assert.Empty(loaded);
    }

    // ── Learned Patterns ────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadPattern_RoundTrips()
    {
        var pattern = new LearnedPattern
        {
            Id = "pattern-1",
            Pattern = "This project uses xUnit, not NUnit",
            Project = "/proj",
            Confidence = 0.85,
        };

        await _store.SavePatternAsync(pattern);
        var loaded = await _store.LoadPatternsAsync("/proj");

        Assert.Single(loaded);
        Assert.Equal("pattern-1", loaded[0].Id);
        Assert.Equal(0.85, loaded[0].Confidence);
    }

    [Fact]
    public async Task LoadPatterns_OrderedByConfidenceDescending()
    {
        await _store.SavePatternAsync(new LearnedPattern { Id = "p1", Pattern = "Low", Project = "/proj", Confidence = 0.3 });
        await _store.SavePatternAsync(new LearnedPattern { Id = "p2", Pattern = "High", Project = "/proj", Confidence = 0.9 });
        await _store.SavePatternAsync(new LearnedPattern { Id = "p3", Pattern = "Mid", Project = "/proj", Confidence = 0.6 });

        var loaded = await _store.LoadPatternsAsync("/proj");

        Assert.Equal(3, loaded.Count);
        Assert.Equal("p2", loaded[0].Id);
        Assert.Equal("p3", loaded[1].Id);
        Assert.Equal("p1", loaded[2].Id);
    }

    [Fact]
    public async Task RemovePattern_DeletesFile()
    {
        await _store.SavePatternAsync(new LearnedPattern { Id = "p1", Pattern = "Gone", Project = "/proj" });
        await _store.RemovePatternAsync("p1", "/proj");

        var loaded = await _store.LoadPatternsAsync("/proj");
        Assert.Empty(loaded);
    }

    // ── Instincts ───────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndLoadInstinct_RoundTrips()
    {
        var instinct = new Instinct
        {
            Id = "instinct-1",
            Trigger = "user corrects test framework",
            Action = "Check project dependencies first",
            Confidence = 0.7,
            Evidence = ["2026-04-05: User corrected xUnit → NUnit"],
        };

        await _store.SaveInstinctAsync(instinct);
        var loaded = await _store.LoadInstinctsAsync(0.0);

        Assert.Single(loaded);
        Assert.Equal("instinct-1", loaded[0].Id);
        Assert.Single(loaded[0].Evidence);
    }

    [Fact]
    public async Task LoadInstincts_FiltersbyMinConfidence()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "A", Action = "B", Confidence = 0.2 });
        await _store.SaveInstinctAsync(new Instinct { Id = "i2", Trigger = "C", Action = "D", Confidence = 0.8 });

        var loaded = await _store.LoadInstinctsAsync(0.5);

        Assert.Single(loaded);
        Assert.Equal("i2", loaded[0].Id);
    }

    [Fact]
    public async Task ReinforceInstinct_IncreasesConfidence()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "A", Action = "B", Confidence = 0.5 });

        await _store.ReinforceInstinctAsync("i1", "Positive feedback");

        var loaded = await _store.LoadInstinctsAsync(0.0);
        Assert.Single(loaded);
        Assert.Equal(0.6, loaded[0].Confidence, precision: 2);
        Assert.Single(loaded[0].Evidence); // reinforcement evidence appended
    }

    [Fact]
    public async Task ReinforceInstinct_CapsAtOne()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "A", Action = "B", Confidence = 0.95 });

        await _store.ReinforceInstinctAsync("i1", "Positive");

        var loaded = await _store.LoadInstinctsAsync(0.0);
        Assert.Equal(1.0, loaded[0].Confidence, precision: 2);
    }

    [Fact]
    public async Task CorrectInstinct_DecreasesConfidence()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "A", Action = "B", Confidence = 0.7 });

        await _store.CorrectInstinctAsync("i1", "Wrong approach");

        var loaded = await _store.LoadInstinctsAsync(0.0);
        Assert.Single(loaded);
        Assert.Equal(0.55, loaded[0].Confidence, precision: 2);
    }

    [Fact]
    public async Task CorrectInstinct_PrunesWhenBelowThreshold()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "A", Action = "B", Confidence = 0.4 });

        await _store.CorrectInstinctAsync("i1", "Bad instinct");

        // 0.4 - 0.15 = 0.25 < 0.3 threshold → pruned
        var loaded = await _store.LoadInstinctsAsync(0.0);
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task RemoveInstinct_DeletesFile()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "A", Action = "B" });
        await _store.RemoveInstinctAsync("i1");

        var loaded = await _store.LoadInstinctsAsync(0.0);
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task PruneInstincts_RemovesBelowThreshold()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "A", Action = "B", Confidence = 0.1 });
        await _store.SaveInstinctAsync(new Instinct { Id = "i2", Trigger = "C", Action = "D", Confidence = 0.2 });
        await _store.SaveInstinctAsync(new Instinct { Id = "i3", Trigger = "E", Action = "F", Confidence = 0.8 });

        var pruned = await _store.PruneInstinctsAsync();

        Assert.Equal(2, pruned);
        var remaining = await _store.LoadInstinctsAsync(0.0);
        Assert.Single(remaining);
        Assert.Equal("i3", remaining[0].Id);
    }

    // ── Slugify ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/home/user/my-project", "home-user-my-project")]
    [InlineData("C:\\Work\\ANANT\\Sovrant", "c-work-anant-sovrant")]
    [InlineData("", "default")]
    [InlineData("  ", "default")]
    public void SlugifyProject_ProducesExpectedSlug(string input, string expected)
    {
        Assert.Equal(expected, FileMemoryStore.SlugifyProject(input));
    }
}
