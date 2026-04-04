using Sovrant.Commands;
using Sovrant.Commands.Commands;

namespace Sovrant.Commands.Tests.Commands;

public sealed class CostCommandTests
{
    [Fact]
    public async Task Cost_ShowsZeroWhenNoTokensRecorded()
    {
        var tracker = new TokenUsageTracker();
        var cmd = new CostCommand(tracker);

        var result = await cmd.ExecuteAsync(string.Empty);

        Assert.NotNull(result.Output);
        Assert.Contains("0", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cost_ShowsRecordedTokenCounts()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record(1_000, 500);
        tracker.Record(2_000, 300);
        var cmd = new CostCommand(tracker);

        var result = await cmd.ExecuteAsync(string.Empty);

        Assert.NotNull(result.Output);
        Assert.Contains("3,000", result.Output, StringComparison.Ordinal); // 3000 input
        Assert.Contains("800",   result.Output, StringComparison.Ordinal); // 800 output
    }

    [Fact]
    public async Task TokenUsageTracker_ResetClearsCounters()
    {
        var tracker = new TokenUsageTracker();
        tracker.Record(500, 250);
        tracker.Reset();

        var cmd = new CostCommand(tracker);
        var result = await cmd.ExecuteAsync(string.Empty);

        Assert.Equal(0, tracker.InputTokens);
        Assert.Equal(0, tracker.OutputTokens);
        Assert.NotNull(result.Output);
        await Task.CompletedTask;
    }
}
