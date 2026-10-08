using System.Runtime.CompilerServices;
using System.Text.Json;
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
/// Phase 148 — older messages are summarised once a request reaches 75% of the model's context
/// window. The newest messages stay word for word, a tool call is never separated from its result,
/// key items are pinned, and the summary is saved so a reopened conversation uses it.
/// </summary>
public sealed class CompactionTests
{
    [Theory]
    [InlineData(80_000, 200_000, false, 150_000)] // 75% of the window
    [InlineData(80_000, null, false, 80_000)]     // window unknown: configured threshold
    [InlineData(50_000, 200_000, true, 50_000)]   // SOVRANT_COMPACT_THRESHOLD set: it wins
    [InlineData(0, 200_000, false, 0)]            // 0 turns compaction off
    public void Threshold_Is_Three_Quarters_Of_The_Window_Unless_Overridden(int configured, int? window, bool env, int expected) =>
        Assert.Equal(expected, ConversationCompaction.Threshold(configured, window, env));

    [Fact]
    public void The_Kept_Part_Never_Starts_On_A_Tool_Result()
    {
        List<InputMessage> history =
        [
            InputMessage.UserText(new string('a', 400)),
            InputMessage.AssistantText(new string('b', 400)),
            InputMessage.UserText(new string('c', 400)),
            ToolCall("t1", "Read"),
            ToolResult("t1", new string('y', 40)),
            InputMessage.AssistantText(new string('z', 20)),
        ];

        // A budget of 25 tokens fits the reply and the tool result but not the call: the cut moves
        // back to keep the call with its result.
        var cut = ConversationCompaction.ChooseCut(history, threshold: 100);

        Assert.Equal(3, cut);
        Assert.Contains(history[cut].Content, b => b is InputContentBlock.ToolUseBlock);
    }

    [Fact]
    public void Requests_The_Approved_Plan_And_The_Latest_Tool_Error_Are_Pinned()
    {
        List<InputMessage> summarised =
        [
            InputMessage.UserText("Build the report"),
            InputMessage.AssistantText("ok"),
            InputMessage.UserText("Now add charts"),
            new("assistant", [new InputContentBlock.TextBlock("Plan: 1. load data 2. draw charts"), Use("p1", "ExitPlanMode")]),
            ToolResult("p1", "Exited Plan mode."),
            ToolCall("t2", "Bash"),
            ToolResult("t2", "command not found: chartgen", isError: true),
        ];

        var pins = ConversationCompaction.Pin(summarised, [InputMessage.AssistantText("Trying another way")], CompactionPins.None);

        Assert.Equal("Build the report", pins.OriginalRequest);
        Assert.Equal("Now add charts", pins.CurrentRequest);
        Assert.Equal("Plan: 1. load data 2. draw charts", pins.ApprovedPlan);
        Assert.Equal("command not found: chartgen", pins.LatestToolError);
        Assert.Equal("Bash", pins.LatestToolErrorTool);

        // The request being worked on isn't pinned when it is among the kept messages.
        var withRequestKept = ConversationCompaction.Pin(summarised, [InputMessage.UserText("Use bars")], CompactionPins.None);
        Assert.Null(withRequestKept.CurrentRequest);
    }

    [Fact]
    public async Task Older_Messages_Are_Summarised_And_A_Reopened_Conversation_Uses_The_Summary()
    {
        var store = new RecordingStore();
        var provider = new SummarisingProvider();
        var first = Runtime(provider, store, threshold: 40);
        await first.InitializeSessionAsync("s-1");

        var request = "Write a long report " + new string('a', 400);
        await foreach (var _ in first.RunTurnAsync(request)) { }
        var events = new List<RuntimeEvent>();
        await foreach (var ev in first.RunTurnAsync("Second " + new string('b', 400))) events.Add(ev);

        // The second request went out as summary + acknowledgement + the new request.
        Assert.Contains(events, e => e is RuntimeEvent.HistoryCompacted);
        var sent = provider.LastChat!;
        Assert.Equal(3, sent.Count);
        Assert.StartsWith(ConversationCompaction.SummaryHeading, Text(sent[0]), StringComparison.Ordinal);
        Assert.Contains("SUMMARY OF EARLIER", Text(sent[0]), StringComparison.Ordinal);
        Assert.Contains("Original request:\n" + request, Text(sent[0]), StringComparison.Ordinal);
        Assert.Equal(ConversationCompaction.Acknowledgement, Text(sent[1]));
        Assert.StartsWith("Second ", Text(sent[2]), StringComparison.Ordinal);

        // The summary is saved with how many saved messages it kept.
        var saved = Assert.Single(store.Entries, e => e.Role == ConversationCompaction.EntryRole);
        using (var doc = JsonDocument.Parse(saved.Content))
            Assert.Equal(1, doc.RootElement.GetProperty("kept").GetInt32());

        // Reopened (threshold high, so no new compaction): the saved summary goes out, not the full history.
        var reopened = Runtime(provider, store, threshold: 1_000_000);
        await reopened.InitializeSessionAsync("s-1");
        await foreach (var _ in reopened.RunTurnAsync("Third")) { }

        var resent = provider.LastChat!.Select(Text).ToList();
        Assert.Equal(5, resent.Count);
        Assert.StartsWith(ConversationCompaction.SummaryHeading, resent[0], StringComparison.Ordinal);
        Assert.Equal(ConversationCompaction.Acknowledgement, resent[1]);
        Assert.StartsWith("Second ", resent[2], StringComparison.Ordinal);
        Assert.Equal("Yes.", resent[3]);
        Assert.Equal("Third", resent[4]);
        Assert.DoesNotContain(resent, t => t.StartsWith("Write a long report", StringComparison.Ordinal));
    }

    [Fact]
    public void Entries_Saved_Before_Phase_148_Are_Ignored_On_Reload() =>
        Assert.Null(ConversationCompaction.Parse("History compacted: 12 messages summarised at 90000 input tokens."));

    private static ConversationRuntime Runtime(ILlmProvider provider, ISessionStore store, int threshold) =>
        new(new OneProvider(provider), new ToolCountCapTests.StubToolExecutor(), new InMemoryToolRegistry(), store,
            new SovrantConfig { Model = "m", CompactThreshold = threshold }, NullLogger<ConversationRuntime>.Instance,
            systemPromptOverride: "sys");

    private static InputContentBlock.ToolUseBlock Use(string id, string name) =>
        new(id, name, JsonDocument.Parse("{}").RootElement.Clone());

    private static InputMessage ToolCall(string id, string name) => new("assistant", [Use(id, name)]);

    private static InputMessage ToolResult(string id, string text, bool isError = false) =>
        new("user", [new InputContentBlock.ToolResultBlock(id, [new ToolResultContentBlock.TextBlock(text)], isError)]);

    private static string Text(InputMessage m) =>
        string.Concat(m.Content.OfType<InputContentBlock.TextBlock>().Select(t => t.Text));

    /// <summary>Answers summary requests with a fixed summary and chat requests with "Yes.".</summary>
    private sealed class SummarisingProvider : ILlmProvider
    {
        public string Name => "fake";
        public Uri BaseUrl => new("http://localhost");
        public IReadOnlyList<InputMessage>? LastChat { get; private set; }

        public Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<StreamEvent> StreamAsync(MessagesRequest req, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var isSummary = req.Messages.Count == 1 && Text(req.Messages[0]).StartsWith("Summarise", StringComparison.Ordinal);
            if (!isSummary) LastChat = req.Messages.ToList();
            yield return new StreamEvent.MessageStart(new MessageResponse("m1", "message", "assistant", [], req.Model, new Usage(InputTokens: 1)));
            yield return new StreamEvent.ContentBlockStart(0, new OutputContentBlock.TextBlock(""));
            yield return new StreamEvent.ContentBlockDelta(0, new ContentBlockDelta.TextDelta(isSummary ? "SUMMARY OF EARLIER" : "Yes."));
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

    /// <summary>A session store that keeps what is appended and loads it back.</summary>
    private sealed class RecordingStore : ISessionStore
    {
        private readonly ToolCountCapTests.InMemorySessionStore _rest = new();
        public List<SessionEntry> Entries { get; } = [];
        public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SessionEntry>>([.. Entries]);
        public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
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
