using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

/// <summary>Tests for <see cref="RuntimeSessionPool"/> with composite keys and scoped router overrides.</summary>
public sealed class RuntimeSessionPoolTests
{
    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SovrantConfig>(new SovrantConfig());
        services.AddSingleton<ISmartRouter>(new FakeRouter());
        services.AddSingleton<IToolExecutor>(new FakeToolExecutor());
        services.AddSingleton<IToolRegistry>(new InMemoryToolRegistry());
        services.AddSingleton<ISessionStore>(new NullSessionStore());
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddTransient<IConversationRuntime, ConversationRuntime>();
        services.AddSingleton<IRuntimeSessionPool, RuntimeSessionPool>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetOrCreateAsync_SameSessionId_ReturnsSameRuntime()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var r1 = await pool.GetOrCreateAsync("session-1");
        var r2 = await pool.GetOrCreateAsync("session-1");

        Assert.Same(r1, r2);
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentSessionId_ReturnsDifferentRuntimes()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var r1 = await pool.GetOrCreateAsync("session-1");
        var r2 = await pool.GetOrCreateAsync("session-2");

        Assert.NotSame(r1, r2);
    }

    [Fact]
    public async Task GetOrCreateAsync_CompositeKey_IsolatesSessionsByProvider()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        // Same logical session ID but different provider tags → different pool entries.
        var r1 = await pool.GetOrCreateAsync("session-1::openai");
        var r2 = await pool.GetOrCreateAsync("session-1::gemini");
        var r3 = await pool.GetOrCreateAsync("session-1::openai");

        Assert.NotSame(r1, r2);
        Assert.Same(r1, r3);
    }

    [Fact]
    public async Task GetOrCreateAsync_WithScopedRouter_UsesOverride()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var scopedRouter = new FakeRouter();
        var runtime = await pool.GetOrCreateAsync("scoped-session", scopedRouter);

        Assert.NotNull(runtime);
        Assert.Equal("scoped-session", runtime.SessionId);
    }

    [Fact]
    public async Task Evict_RemovesSession()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var r1 = await pool.GetOrCreateAsync("session-evict");
        pool.Evict("session-evict");
        var r2 = await pool.GetOrCreateAsync("session-evict");

        Assert.NotSame(r1, r2);
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class FakeRouter : ISmartRouter
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default)
            => Task.FromResult<ILlmProvider>(new FakeProvider());
        public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default)
            => Task.CompletedTask;
        public IReadOnlyList<ProviderStatus> GetStatus() => [];
        public Task PinProviderAsync(string? providerName, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProvider : ILlmProvider
    {
        public string Name => "fake";
        public Uri BaseUrl => new("http://localhost");
        public Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default)
            => throw new NotSupportedException();
        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            MessagesRequest req, [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new StreamEvent.MessageStop();
            await Task.CompletedTask;
        }
    }

    private sealed class FakeToolExecutor : IToolExecutor
    {
        public Task<ToolExecutionResult> ExecuteAsync(string toolName, System.Text.Json.JsonElement input, CancellationToken ct = default)
            => Task.FromResult(new ToolExecutionResult(true, "ok"));
    }

    private sealed class NullSessionStore : ISessionStore
    {
        public Task AppendAsync(string sessionId, SessionEntry entry, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionEntry>>([]);
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
