using Sovrant.Commands;
using Sovrant.Commands.Commands;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;

namespace Sovrant.Commands.Tests.Commands;

public sealed class ExitClearCommandTests
{
    // ── ExitCommand ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Exit_ReturnsShouldExit()
    {
        var cmd = new ExitCommand();
        var result = await cmd.ExecuteAsync(string.Empty);
        Assert.True(result.ShouldExit);
    }

    [Fact]
    public async Task Exit_HasAliasesQuitAndQ()
    {
        var cmd = new ExitCommand();
        Assert.Contains("quit", cmd.Aliases, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("q", cmd.Aliases, StringComparer.OrdinalIgnoreCase);
        // Suppress unused warning
        await Task.CompletedTask;
    }

    // ── ClearCommand ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Clear_CallsRuntimeReset()
    {
        var runtime = new FakeRuntime();
        var tracker = new TokenUsageTracker();
        tracker.Record(100, 50);

        var cmd = new ClearCommand(runtime, tracker);
        var result = await cmd.ExecuteAsync(string.Empty);

        Assert.True(runtime.WasReset);
        Assert.True(result.ShouldClearHistory);
        // Tracker should also be reset
        Assert.Equal(0, tracker.InputTokens);
    }

    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed class FakeRuntime : IConversationRuntime
    {
        public string SessionId => "fake-session";
        public bool WasReset { get; private set; }

        public Task InitializeSessionAsync(string? sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void Reset() => WasReset = true;

#pragma warning disable CS1998 // async method without await
        public async IAsyncEnumerable<RuntimeEvent> RunTurnAsync(
            string userMessage,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield break;
        }
#pragma warning restore CS1998
    }
}
