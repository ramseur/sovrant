using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Memory;

namespace Sovrant.Runtime.Tests.Memory;

public sealed class MemoryInjectorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileMemoryStore _store;
    private readonly MemoryInjector _injector;

    public MemoryInjectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _store = new FileMemoryStore(
            Path.Combine(_tempDir, "summaries"),
            Path.Combine(_tempDir, "learned"),
            Path.Combine(_tempDir, "instincts"),
            NullLogger<FileMemoryStore>.Instance);

        _injector = new MemoryInjector(_store, NullLogger<MemoryInjector>.Instance);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public async Task BuildMemorySection_NoMemories_ReturnsEmpty()
    {
        var result = await _injector.BuildMemorySectionAsync("/nonexistent");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task BuildMemorySection_IncludesSummaries()
    {
        await _store.SaveSummaryAsync(new SessionSummary
        {
            SessionId = "s1",
            Project = "/proj",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAt = DateTimeOffset.UtcNow,
            Tasks = ["Fix the auth bug"],
            ToolsUsed = ["bash", "edit_file"],
            Outcome = SessionOutcome.Success,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("Recent sessions", result);
        Assert.Contains("Fix the auth bug", result);
        Assert.Contains("bash", result);
    }

    [Fact]
    public async Task BuildMemorySection_IncludesPatterns()
    {
        await _store.SavePatternAsync(new LearnedPattern
        {
            Id = "p1",
            Pattern = "This project uses xUnit",
            Project = "/proj",
            Confidence = 0.9,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("Learned patterns", result);
        Assert.Contains("This project uses xUnit", result);
    }

    [Fact]
    public async Task BuildMemorySection_IncludesInstincts()
    {
        await _store.SaveInstinctAsync(new Instinct
        {
            Id = "i1",
            Trigger = "user mentions testing",
            Action = "Check test framework first",
            Confidence = 0.8,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("Behavioral instincts", result);
        Assert.Contains("user mentions testing", result);
        Assert.Contains("Check test framework first", result);
    }

    [Fact]
    public async Task BuildMemorySection_ExcludesLowConfidenceInstincts()
    {
        await _store.SaveInstinctAsync(new Instinct
        {
            Id = "i1",
            Trigger = "low confidence trigger",
            Action = "Should not appear",
            Confidence = 0.35, // Below the 0.4 threshold in MemoryInjector
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.DoesNotContain("low confidence trigger", result);
    }

    [Fact]
    public async Task BuildMemorySection_ShowsConfidenceForLowPatterns()
    {
        await _store.SavePatternAsync(new LearnedPattern
        {
            Id = "p1",
            Pattern = "Uncertain pattern",
            Project = "/proj",
            Confidence = 0.5,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("confidence: 0.5", result);
    }

    [Fact]
    public async Task BuildMemorySection_AllThreeLayers()
    {
        await _store.SaveSummaryAsync(new SessionSummary
        {
            SessionId = "s1",
            Project = "/proj",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAt = DateTimeOffset.UtcNow,
            Tasks = ["Task one"],
            ToolsUsed = ["bash"],
            Outcome = SessionOutcome.Success,
        });

        await _store.SavePatternAsync(new LearnedPattern
        {
            Id = "p1",
            Pattern = "Uses NUnit",
            Project = "/proj",
            Confidence = 0.9,
        });

        await _store.SaveInstinctAsync(new Instinct
        {
            Id = "i1",
            Trigger = "test gen",
            Action = "Use NUnit",
            Confidence = 0.8,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("Recent sessions", result);
        Assert.Contains("Learned patterns", result);
        Assert.Contains("Behavioral instincts", result);
    }
}
