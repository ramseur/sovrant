using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>
/// Verifies that <see cref="McpClientRegistry"/> gating correctly hides or exposes
/// MCP-sourced tools based on <see cref="SessionConfig.AllowedMcpServers"/>.
/// No real LLM or MCP network calls are made.
/// </summary>
public sealed class McpSessionGatingTests
{
    private const string NativeTool = "list_files";
    private const string McpTool = "pixellab_generate_image";
    private const string McpServer = "pixellab";

    // A long message that triggers LooksLikeToolRequest (contains "list", "file")
    private const string ToolLikeMessage = "list files in the current directory for me";

    // ── Fakes ─────────────────────────────────────────────────────────────

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
        private readonly List<SessionEntry> _entries = [];
        public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default)
        { _entries.Add(entry); return Task.CompletedTask; }
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
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static ToolDefinition MakeToolDef(string name) =>
        new(name, JsonDocument.Parse("{}").RootElement);

    private static (ConversationRuntime Runtime, CapturingProvider Provider) BuildRuntime(
        McpClientRegistry mcpRegistry)
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(MakeToolDef(NativeTool), (_, _) => Task.FromResult("[]"));
        registry.Register(MakeToolDef(McpTool), (_, _) => Task.FromResult("{}"));

        var router = new FakeRouter();
        var runtime = new ConversationRuntime(
            router: router,
            toolExecutor: new StubToolExecutor(),
            toolRegistry: registry,
            sessionStore: new InMemorySessionStore(),
            config: new SovrantConfig { Model = "test-model" },
            logger: NullLogger<ConversationRuntime>.Instance,
            mcpClients: mcpRegistry);

        return (runtime, router.Provider);
    }

    private static McpClientRegistry MakeRegistry()
    {
        var reg = new McpClientRegistry();
        reg.MapTool(McpTool, McpServer);
        return reg;
    }

    private static async Task<HashSet<string>> RunAndGetToolNames(
        ConversationRuntime runtime, SessionConfig config)
    {
        using var _ = SessionContext.Push(config);
        await foreach (var __ in runtime.RunTurnAsync(ToolLikeMessage)) { }
        return [];  // tool names come from the provider, captured separately
    }

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoConnectionsSelected_McpToolsHiddenFromModel()
    {
        var (runtime, provider) = BuildRuntime(MakeRegistry());
        await runtime.InitializeSessionAsync("sess-1");

        using var _ = SessionContext.Push(new SessionConfig { AllowedMcpServers = [] });
        await foreach (var __ in runtime.RunTurnAsync(ToolLikeMessage)) { }

        Assert.NotNull(provider.LastTools);
        var names = provider.LastTools!.Select(t => t.Name).ToHashSet();
        Assert.DoesNotContain(McpTool, names);
        Assert.Contains(NativeTool, names);
    }

    [Fact]
    public async Task PixellabSelected_McpToolsVisibleToModel()
    {
        var (runtime, provider) = BuildRuntime(MakeRegistry());
        await runtime.InitializeSessionAsync("sess-2");

        using var _ = SessionContext.Push(new SessionConfig { AllowedMcpServers = [McpServer] });
        await foreach (var __ in runtime.RunTurnAsync(ToolLikeMessage)) { }

        Assert.NotNull(provider.LastTools);
        var names = provider.LastTools!.Select(t => t.Name).ToHashSet();
        Assert.Contains(McpTool, names);
        Assert.Contains(NativeTool, names);
    }

    [Fact]
    public async Task NullAllowList_AllToolsVisibleToModel()
    {
        var (runtime, provider) = BuildRuntime(MakeRegistry());
        await runtime.InitializeSessionAsync("sess-3");

        // null = "no gating" — every connected server's tools are available
        using var _ = SessionContext.Push(new SessionConfig { AllowedMcpServers = null });
        await foreach (var __ in runtime.RunTurnAsync(ToolLikeMessage)) { }

        Assert.NotNull(provider.LastTools);
        var names = provider.LastTools!.Select(t => t.Name).ToHashSet();
        Assert.Contains(McpTool, names);
        Assert.Contains(NativeTool, names);
    }

    [Fact]
    public async Task UnrelatedServerSelected_McpToolsHiddenFromModel()
    {
        var (runtime, provider) = BuildRuntime(MakeRegistry());
        await runtime.InitializeSessionAsync("sess-4");

        // User selects a different server — pixellab tools must be dropped
        using var _ = SessionContext.Push(new SessionConfig { AllowedMcpServers = ["github"] });
        await foreach (var __ in runtime.RunTurnAsync(ToolLikeMessage)) { }

        Assert.NotNull(provider.LastTools);
        var names = provider.LastTools!.Select(t => t.Name).ToHashSet();
        Assert.DoesNotContain(McpTool, names);
        Assert.Contains(NativeTool, names);
    }
}
