using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Tests.Engine;

public sealed class NaiveContextCompactorTests
{
    private static StepOutcome Make(
        int index,
        StepStatus status = StepStatus.Succeeded,
        string summary = "ok",
        string? error = null)
    {
        return new StepOutcome(
            StepIndex: index,
            Status: status,
            Summary: summary,
            ObservationJson: null,
            MatchedExpectation: status == StepStatus.Succeeded,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            ErrorMessage: error);
    }

    [Fact]
    public async Task Compact_ReturnsInputUnchanged_WhenUnderBudget()
    {
        var compactor = new NaiveContextCompactor();
        var outcomes = new[] { Make(0), Make(1), Make(2) };

        var result = await compactor.CompactAsync(outcomes, budget: 100_000);

        Assert.Equal(0, result.FoldedCount);
        Assert.Equal(string.Empty, result.Summary);
        Assert.Same(outcomes, result.PreservedOutcomes);
    }

    [Fact]
    public async Task Compact_FoldsOlderOutcomes_WhenOverBudget()
    {
        var compactor = new NaiveContextCompactor { MinKeepRecent = 2 };
        // Eight outcomes with moderately long summaries.
        var outcomes = Enumerable.Range(0, 8)
            .Select(i => Make(i, summary: new string('x', 80)))
            .ToArray();

        var result = await compactor.CompactAsync(outcomes, budget: 400);

        Assert.True(result.FoldedCount > 0, "expected some outcomes to be folded");
        Assert.True(
            result.PreservedOutcomes.Count >= 2,
            "should keep at least MinKeepRecent outcomes");
        Assert.Equal(outcomes.Length, result.FoldedCount + result.PreservedOutcomes.Count);

        // PreservedOutcomes must be a tail of the input.
        var tail = outcomes.Skip(outcomes.Length - result.PreservedOutcomes.Count).ToArray();
        Assert.Equal(tail, result.PreservedOutcomes);

        Assert.StartsWith("Compacted", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compact_AlwaysKeepsAtLeastMinKeepRecent()
    {
        var compactor = new NaiveContextCompactor { MinKeepRecent = 3 };
        // Ten outcomes but a very small budget — compactor must still honour
        // the MinKeepRecent floor instead of folding everything.
        var outcomes = Enumerable.Range(0, 10)
            .Select(i => Make(i, summary: new string('y', 50)))
            .ToArray();

        var result = await compactor.CompactAsync(outcomes, budget: 50);

        Assert.True(result.PreservedOutcomes.Count >= 3);
    }

    [Fact]
    public async Task Compact_SummaryIncludesFailedErrorMessage()
    {
        var compactor = new NaiveContextCompactor { MinKeepRecent = 1 };
        var outcomes = new[]
        {
            Make(0, StepStatus.Failed, summary: "attempt 1", error: "disk full"),
            Make(1, StepStatus.Succeeded, summary: new string('z', 200)),
            Make(2, StepStatus.Succeeded, summary: new string('z', 200)),
        };

        var result = await compactor.CompactAsync(outcomes, budget: 300);

        Assert.True(result.FoldedCount >= 1);
        Assert.Contains("disk full", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compact_EmptyList_ReturnsEmpty()
    {
        var compactor = new NaiveContextCompactor();
        var result = await compactor.CompactAsync(Array.Empty<StepOutcome>(), budget: 100);

        Assert.Equal(0, result.FoldedCount);
        Assert.Empty(result.PreservedOutcomes);
        Assert.Equal(string.Empty, result.Summary);
    }

    [Fact]
    public async Task Compact_NullOutcomes_Throws()
    {
        var compactor = new NaiveContextCompactor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => compactor.CompactAsync(null!, budget: 100));
    }

    [Fact]
    public async Task Compact_ZeroBudget_Throws()
    {
        var compactor = new NaiveContextCompactor();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => compactor.CompactAsync(Array.Empty<StepOutcome>(), budget: 0));
    }
}
