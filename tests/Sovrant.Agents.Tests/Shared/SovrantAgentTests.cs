using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Agents.Tests.Shared;

public sealed class SovrantAgentTests
{
    [Fact]
    public async Task HandleAsync_Collects_Text_Output()
    {
        var runtime = new FakeConversationRuntime(
        [
            new RuntimeEvent.TextChunk("Hello "),
            new RuntimeEvent.TextChunk("World"),
            new RuntimeEvent.TurnComplete("end_turn", 100, 50),
        ]);

        var agent = new SovrantAgent("test-agent", AgentRole.General, runtime, NullLogger.Instance);
        var task = AgentTask.Create("say hello");

        var result = await agent.HandleAsync(task);

        Assert.True(result.Success);
        Assert.Equal("Hello World", result.Output);
    }

    [Fact]
    public async Task HandleAsync_Returns_Fail_On_RuntimeError()
    {
        var runtime = new FakeConversationRuntime(
        [
            new RuntimeEvent.TextChunk("partial"),
            new RuntimeEvent.RuntimeError("Provider unavailable"),
        ]);

        var agent = new SovrantAgent("test-agent", AgentRole.General, runtime, NullLogger.Instance);
        var task = AgentTask.Create("do something");

        var result = await agent.HandleAsync(task);

        Assert.False(result.Success);
        Assert.Equal("Provider unavailable", result.Error);
    }

    [Fact]
    public async Task HandleAsync_Returns_Fail_On_Cancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Use a runtime that checks cancellation before yielding
        var runtime = new CancellingConversationRuntime();
        var agent = new SovrantAgent("test-agent", AgentRole.General, runtime, NullLogger.Instance);
        var task = AgentTask.Create("cancelled");

        var result = await agent.HandleAsync(task, cts.Token);

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_No_Output_Returns_Placeholder()
    {
        var runtime = new FakeConversationRuntime(
        [
            new RuntimeEvent.TurnComplete("end_turn", 10, 5),
        ]);

        var agent = new SovrantAgent("test-agent", AgentRole.General, runtime, NullLogger.Instance);
        var task = AgentTask.Create("empty");

        var result = await agent.HandleAsync(task);

        Assert.True(result.Success);
        Assert.Equal("(agent returned no output)", result.Output);
    }

    /// <summary>Fake runtime that yields pre-configured events.</summary>
    private sealed class FakeConversationRuntime : IConversationRuntime
    {
        private readonly RuntimeEvent[] _events;

        public FakeConversationRuntime(RuntimeEvent[] events) => _events = events;

        public string SessionId => "fake-session";

        public Task InitializeSessionAsync(string? sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public async IAsyncEnumerable<RuntimeEvent> RunTurnAsync(
            string userMessage,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var ev in _events)
            {
                ct.ThrowIfCancellationRequested();
                yield return ev;
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public void Reset() { }
    }

    /// <summary>Fake runtime that always throws OperationCanceledException.</summary>
    private sealed class CancellingConversationRuntime : IConversationRuntime
    {
        public string SessionId => "fake-session";

        public Task InitializeSessionAsync(string? sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public async IAsyncEnumerable<RuntimeEvent> RunTurnAsync(
            string userMessage,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            yield return new RuntimeEvent.TextChunk("unreachable");
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public void Reset() { }
    }
}
