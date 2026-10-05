using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;
using static Sovrant.Runtime.Tests.Conversation.ToolCountCapTests;

namespace Sovrant.Runtime.Tests.Conversation;

/// <summary>
/// A model call that "succeeds" with no text, no tool calls and no output tokens (seen with busy
/// :free models on OpenRouter) used to end the turn silently, so the user had to send the prompt
/// again. It's now retried automatically, and becomes a visible error if every attempt is empty.
/// </summary>
public sealed class EmptyResponseTests
{
    /// <summary>Returns an empty reply for the first <c>emptyCalls</c> calls, then "ok".</summary>
    private sealed class FlakyProvider(int emptyCalls) : ILlmProvider
    {
        public int Calls { get; private set; }
        public string Name => "fake";
        public Uri BaseUrl => new("https://openrouter.ai/api/v1/");
        public Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<StreamEvent> StreamAsync(MessagesRequest req, [EnumeratorCancellation] CancellationToken ct = default)
        {
            Calls++;
            yield return new StreamEvent.MessageStart(new MessageResponse("m", "message", "assistant", [], "free-model", new Usage(0)));
            if (Calls > emptyCalls)
            {
                yield return new StreamEvent.ContentBlockStart(0, new OutputContentBlock.TextBlock(""));
                yield return new StreamEvent.ContentBlockDelta(0, new ContentBlockDelta.TextDelta("ok"));
                yield return new StreamEvent.ContentBlockStop(0);
                yield return new StreamEvent.MessageDelta(new Sovrant.Api.Types.MessageDelta("end_turn", null), new Usage(InputTokens: 10, OutputTokens: 1));
            }
            yield return new StreamEvent.MessageStop();
            await Task.CompletedTask;
        }
    }

    private sealed class SingleProviderRouter(ILlmProvider provider) : ISmartRouter
    {
        public bool IntentRoutingEnabled { get; set; }
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) => Task.FromResult(provider);
        public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) => Task.CompletedTask;
        public IReadOnlyList<ProviderStatus> GetStatus() => [];
        public Task PinProviderAsync(string? providerName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default)
            => Task.FromResult(new RoutingDecision(provider, null, null, null));
    }

    private static async Task<List<RuntimeEvent>> RunTurn(FlakyProvider provider)
    {
        var runtime = new ConversationRuntime(
            router: new SingleProviderRouter(provider),
            toolExecutor: new StubToolExecutor(),
            toolRegistry: new InMemoryToolRegistry(),
            sessionStore: new InMemorySessionStore(),
            config: new SovrantConfig { Model = "nvidia/nemotron:free" },
            logger: NullLogger<ConversationRuntime>.Instance);
        await runtime.InitializeSessionAsync("sess-empty");
        using var _ = SessionContext.Push(new SessionConfig());
        var events = new List<RuntimeEvent>();
        await foreach (var e in runtime.RunTurnAsync("hi"))
            events.Add(e);
        return events;
    }

    [Fact]
    public async Task One_Empty_Reply_Is_Retried_And_The_User_Gets_The_Answer()
    {
        var provider = new FlakyProvider(emptyCalls: 1);

        var events = await RunTurn(provider);

        Assert.Equal(2, provider.Calls);
        Assert.Equal("ok", string.Concat(events.OfType<RuntimeEvent.TextChunk>().Select(t => t.Text)));
        Assert.DoesNotContain(events, e => e is RuntimeEvent.RuntimeError);
    }

    [Fact]
    public async Task Every_Attempt_Empty_Becomes_A_Visible_Error_Not_A_Silent_Turn()
    {
        var provider = new FlakyProvider(emptyCalls: int.MaxValue);

        var events = await RunTurn(provider);

        Assert.Equal(3, provider.Calls); // the runtime's existing 3-attempt retry
        var error = Assert.Single(events.OfType<RuntimeEvent.RuntimeError>());
        Assert.Contains(ConversationRuntime.EmptyResponseError, error.Message, StringComparison.Ordinal);
    }
}
