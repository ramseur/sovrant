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
/// Regression coverage for the tool-count cap in
/// <see cref="ConversationRuntime.FilterToolsForModel"/>. OpenAI's Chat
/// Completions API — and every OpenAI-compatible provider behind it
/// (OpenRouter, Ollama, etc.) — rejects requests with more than 128 tool
/// definitions with a 400. Without a fallback cap, a registry with more
/// tools than that (built-in tools + enabled MCP servers) got sent
/// unbounded whenever no per-model <c>MaxTools</c> override was configured,
/// which is the case for effectively every model today.
/// </summary>
public sealed class ToolCountCapTests
{
    private const string ToolLikeMessage = "list files in the current directory for me";

    private sealed class CapturingProvider : ILlmProvider
    {
        public string Name => "fake";
        public Uri BaseUrl => new("http://localhost");
        public IReadOnlyList<ToolDefinition>? LastTools { get; private set; }

        public Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            MessagesRequest req,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            LastTools = req.Tools;
            yield return new StreamEvent.MessageStart(new MessageResponse(
                Id: "msg-test",
                Type: "message",
                Role: "assistant",
                Content: [],
                Model: "test-model",
                Usage: new Usage(InputTokens: 10)));
            yield return new StreamEvent.ContentBlockStart(0, new OutputContentBlock.TextBlock(""));
            yield return new StreamEvent.ContentBlockDelta(0, new ContentBlockDelta.TextDelta("ok"));
            yield return new StreamEvent.ContentBlockStop(0);
            yield return new StreamEvent.MessageDelta(
                new Sovrant.Api.Types.MessageDelta("end_turn", null),
                new Usage(InputTokens: 10, OutputTokens: 2));
            yield return new StreamEvent.MessageStop();
            await Task.CompletedTask;
        }
    }

    private sealed class FakeRouter : ISmartRouter
    {
        public CapturingProvider Provider { get; } = new();
        public bool IntentRoutingEnabled { get; set; }

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default)
            => Task.FromResult<ILlmProvider>(Provider);
        public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default)
            => Task.CompletedTask;
        public IReadOnlyList<ProviderStatus> GetStatus() => [];
        public Task PinProviderAsync(string? providerName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default)
            => Task.FromResult(new RoutingDecision(Provider, null, null, null));
    }

    private sealed class StubToolExecutor : IToolExecutor
    {
        public Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct = default)
            => Task.FromResult(new ToolExecutionResult(true, "[]"));
    }

    private sealed class InMemorySessionStore : ISessionStore
    {
        public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionEntry>>([]);
        public Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<int> DeleteAllAsync(CancellationToken ct = default)
            => Task.FromResult(0);
        public Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
        public Task SetTitleAsync(string sessionId, string title, string? ownerUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<string?> GetTitleAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<SessionListItem>> ListWithTitlesAsync(string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionListItem>>([]);
        public Task<IReadOnlyList<SessionListItem>> SearchAsync(string query, string? ownerUserId = null, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionListItem>>([]);
        public Task<IReadOnlyList<string>?> GetMcpConnectionsAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>?>(null);
        public Task SetMcpConnectionsAsync(string sessionId, IReadOnlyList<string>? servers, string? ownerUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task UpdatePrivacyAsync(string sessionId, string ownerUserId, bool isPrivate, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<bool?> GetIsPrivateAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<bool?>(null);
        public Task SetAgentNameAsync(string sessionId, string agentName, string? ownerUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<string?> GetAgentNameAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    private static ToolDefinition MakeToolDef(string name) =>
        new(name, JsonDocument.Parse("{}").RootElement);

    [Fact]
    public async Task ToolRegistryOver128_IsCappedBeforeSendingToProvider()
    {
        // Reproduces the reported bug: 144 registered tools (58 built-in + enabled
        // MCP servers) sent unbounded to an OpenAI-compatible endpoint, which
        // hard-rejects with "Invalid 'tools': array too long... maximum length 128".
        var registry = new InMemoryToolRegistry();
        for (var i = 0; i < 144; i++)
            registry.Register(MakeToolDef($"tool_{i}"), (_, _) => Task.FromResult("[]"));

        var router = new FakeRouter();
        var runtime = new ConversationRuntime(
            router: router,
            toolExecutor: new StubToolExecutor(),
            toolRegistry: registry,
            sessionStore: new InMemorySessionStore(),
            config: new SovrantConfig { Model = "test-model" },
            logger: NullLogger<ConversationRuntime>.Instance);

        await runtime.InitializeSessionAsync("sess-tool-cap");
        using var _ = SessionContext.Push(new SessionConfig());
        await foreach (var __ in runtime.RunTurnAsync(ToolLikeMessage)) { }

        Assert.NotNull(router.Provider.LastTools);
        Assert.True(router.Provider.LastTools!.Count <= 128,
            $"Expected at most 128 tools sent to the provider, got {router.Provider.LastTools.Count}.");
    }
}
