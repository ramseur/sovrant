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

namespace Sovrant.Runtime.Tests.Conversation;

/// <summary>
/// A request whose turn failed (no reply) must not be sent back to the model later. Reported case:
/// a conversation held six unanswered "make a pdf on using ai at non profits" messages from a turn
/// that had failed weeks earlier; replying "are you here" made the model generate that PDF.
/// </summary>
public sealed class UnansweredRequestTests
{
    [Fact]
    public async Task A_Resumed_Conversation_Sends_Only_Answered_Messages_Plus_The_New_One()
    {
        var store = new StoreWith(
            ("user", "are u here?"), ("user", "are u here?"), ("user", "are you here"),
            ("assistant", "Yes, I'm here — what can I help with?"),
            ("user", "make a pdf on using ai at non profits"), ("user", "make a pdf on using ai at non profits"),
            ("user", "make a pdf on using ai at non profits"), ("user", "are you here"), ("user", "are you here?"));
        var provider = new RecordingProvider();
        var runtime = new ConversationRuntime(new OneProvider(provider), new ToolCountCapTests.StubToolExecutor(),
            new InMemoryToolRegistry(), store, new SovrantConfig { Model = "m" }, NullLogger<ConversationRuntime>.Instance);

        await runtime.InitializeSessionAsync("s-1");
        await foreach (var _ in runtime.RunTurnAsync("are you here")) { }

        var sent = provider.LastMessages!.Select(m => (m.Role, Text(m))).ToList();
        Assert.Equal(
            [("user", "are you here"), ("assistant", "Yes, I'm here — what can I help with?"), ("user", "are you here")],
            sent);
        Assert.DoesNotContain(sent, m => m.Item2.Contains("pdf", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_Failed_Turn_In_The_Same_Session_Is_Not_Sent_With_The_Next_Message()
    {
        var provider = new RecordingProvider { FailNext = true };
        var runtime = new ConversationRuntime(new OneProvider(provider), new ToolCountCapTests.StubToolExecutor(),
            new InMemoryToolRegistry(), new ToolCountCapTests.InMemorySessionStore(), new SovrantConfig { Model = "m" },
            NullLogger<ConversationRuntime>.Instance);

        try { await foreach (var _ in runtime.RunTurnAsync("make a pdf")) { } }
        catch (InvalidOperationException) { /* the failed turn */ }

        await foreach (var _ in runtime.RunTurnAsync("are you here")) { }

        Assert.Equal(["are you here"], provider.LastMessages!.Select(Text));
    }

    private static string Text(InputMessage m) =>
        string.Concat(m.Content.OfType<InputContentBlock.TextBlock>().Select(t => t.Text));

    private sealed class RecordingProvider : ILlmProvider
    {
        public string Name => "fake";
        public Uri BaseUrl => new("http://localhost");
        public IReadOnlyList<InputMessage>? LastMessages { get; private set; }
        public bool FailNext { get; set; }

        public Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<StreamEvent> StreamAsync(MessagesRequest req, [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("provider down");
            }
            LastMessages = req.Messages.ToList();
            yield return new StreamEvent.MessageStart(new MessageResponse("m1", "message", "assistant", [], req.Model, new Usage(InputTokens: 1)));
            yield return new StreamEvent.ContentBlockStart(0, new OutputContentBlock.TextBlock(""));
            yield return new StreamEvent.ContentBlockDelta(0, new ContentBlockDelta.TextDelta("Yes."));
            yield return new StreamEvent.ContentBlockStop(0);
            yield return new StreamEvent.MessageDelta(new Sovrant.Api.Types.MessageDelta("end_turn", null), new Usage(InputTokens: 1, OutputTokens: 1));
            yield return new StreamEvent.MessageStop();
            await Task.CompletedTask;
        }
    }

    private sealed class OneProvider(ILlmProvider provider) : ISmartRouter
    {
        public bool IntentRoutingEnabled { get; set; }
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) => Task.FromResult(provider);
        public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) => Task.CompletedTask;
        public IReadOnlyList<ProviderStatus> GetStatus() => [];
        public Task PinProviderAsync(string? providerName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default) =>
            Task.FromResult(new RoutingDecision(provider, null, null, null));
    }

    /// <summary>A session store that returns a saved conversation.</summary>
    private sealed class StoreWith(params (string Role, string Content)[] entries) : ISessionStore
    {
        private readonly ToolCountCapTests.InMemorySessionStore _rest = new();
        public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SessionEntry>>(entries.Select((e, i) => new SessionEntry($"e{i}", DateTimeOffset.UtcNow, e.Role, e.Content)).ToList());
        public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default) => _rest.ListAsync(ownerUserId, ct);
        public Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default) => _rest.DeleteAsync(sessionId, ownerUserId, ct);
        public Task<int> DeleteAllAsync(CancellationToken ct = default) => _rest.DeleteAllAsync(ct);
        public Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default) => _rest.GetOwnerAsync(sessionId, ct);
        public Task SetTitleAsync(string sessionId, string title, string? ownerUserId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> GetTitleAsync(string sessionId, CancellationToken ct = default) => _rest.GetTitleAsync(sessionId, ct);
        public Task<IReadOnlyList<SessionListItem>> ListWithTitlesAsync(string? ownerUserId = null, CancellationToken ct = default) => _rest.ListWithTitlesAsync(ownerUserId, ct);
        public Task<IReadOnlyList<SessionListItem>> SearchAsync(string query, string? ownerUserId = null, int limit = 50, CancellationToken ct = default) => _rest.SearchAsync(query, ownerUserId, limit, ct);
        public Task<IReadOnlyList<string>?> GetMcpConnectionsAsync(string sessionId, CancellationToken ct = default) => _rest.GetMcpConnectionsAsync(sessionId, ct);
        public Task SetMcpConnectionsAsync(string sessionId, IReadOnlyList<string>? servers, string? ownerUserId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdatePrivacyAsync(string sessionId, string ownerUserId, bool isPrivate, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool?> GetIsPrivateAsync(string sessionId, CancellationToken ct = default) => _rest.GetIsPrivateAsync(sessionId, ct);
        public Task SetAgentNameAsync(string sessionId, string agentName, string? ownerUserId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> GetAgentNameAsync(string sessionId, CancellationToken ct = default) => _rest.GetAgentNameAsync(sessionId, ct);
    }
}
