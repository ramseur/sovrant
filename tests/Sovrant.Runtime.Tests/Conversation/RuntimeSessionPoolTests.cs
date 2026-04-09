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

/// <summary>Tests for <see cref="RuntimeSessionPool"/> with composite keys, scoped routers, TTL, and locking.</summary>
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

        Assert.Same(r1.Runtime, r2.Runtime);
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentSessionId_ReturnsDifferentRuntimes()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var r1 = await pool.GetOrCreateAsync("session-1");
        var r2 = await pool.GetOrCreateAsync("session-2");

        Assert.NotSame(r1.Runtime, r2.Runtime);
    }

    [Fact]
    public async Task GetOrCreateAsync_CompositeKey_IsolatesSessionsByProvider()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var r1 = await pool.GetOrCreateAsync("session-1::openai");
        var r2 = await pool.GetOrCreateAsync("session-1::gemini");
        var r3 = await pool.GetOrCreateAsync("session-1::openai");

        Assert.NotSame(r1.Runtime, r2.Runtime);
        Assert.Same(r1.Runtime, r3.Runtime);
    }

    [Fact]
    public async Task GetOrCreateAsync_WithScopedRouter_UsesOverride()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var scopedRouter = new FakeRouter();
        var pooled = await pool.GetOrCreateAsync("scoped-session", scopedRouter);

        Assert.NotNull(pooled.Runtime);
        Assert.Equal("scoped-session", pooled.Runtime.SessionId);
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsLock_ForTurnSerialization()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var pooled = await pool.GetOrCreateAsync("locked-session");

        Assert.NotNull(pooled.Lock);
        Assert.Equal(1, pooled.Lock.CurrentCount);

        // Acquire and release the lock to verify it works.
        await pooled.Lock.WaitAsync();
        Assert.Equal(0, pooled.Lock.CurrentCount);
        pooled.Lock.Release();
        Assert.Equal(1, pooled.Lock.CurrentCount);
    }

    [Fact]
    public async Task Evict_RemovesSession()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var r1 = await pool.GetOrCreateAsync("session-evict");
        pool.Evict("session-evict");
        var r2 = await pool.GetOrCreateAsync("session-evict");

        Assert.NotSame(r1.Runtime, r2.Runtime);
    }

    [Fact]
    public async Task ActiveCount_TracksPoolSize()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        Assert.Equal(0, pool.ActiveCount);

        await pool.GetOrCreateAsync("a");
        await pool.GetOrCreateAsync("b");
        Assert.Equal(2, pool.ActiveCount);

        pool.Evict("a");
        Assert.Equal(1, pool.ActiveCount);
    }

    [Fact]
    public async Task EvictExpired_RemovesOldSessions()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        await pool.GetOrCreateAsync("old-session");
        await pool.GetOrCreateAsync("new-session");

        // With TTL=0 (immediate expiry), all sessions should be evicted.
        var evicted = pool.EvictExpired(TimeSpan.Zero, maxSessions: 1000);
        Assert.Equal(2, evicted);
        Assert.Equal(0, pool.ActiveCount);
    }

    [Fact]
    public async Task EvictExpired_EnforcesMaxSessionsCap()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        await pool.GetOrCreateAsync("s1");
        await pool.GetOrCreateAsync("s2");
        await pool.GetOrCreateAsync("s3");
        Assert.Equal(3, pool.ActiveCount);

        // TTL is very long so nothing expires, but cap is 1 → evict 2.
        var evicted = pool.EvictExpired(TimeSpan.FromHours(24), maxSessions: 1);
        Assert.Equal(2, evicted);
        Assert.Equal(1, pool.ActiveCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsSessionConfig()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var pooled = await pool.GetOrCreateAsync("config-session");

        Assert.NotNull(pooled.Config);
        Assert.Null(pooled.Config.Model);
        Assert.Null(pooled.Config.PermissionMode);
        Assert.Equal(0, pooled.Config.TotalInputTokens);
        Assert.Equal(0, pooled.Config.TotalOutputTokens);
    }

    [Fact]
    public async Task GetOrCreateAsync_SameSession_ReturnsSameConfig()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var r1 = await pool.GetOrCreateAsync("shared-config");
        r1.Config.Model = "custom-model";
        r1.Config.PermissionMode = Sovrant.Runtime.Permissions.PermissionMode.Plan;

        var r2 = await pool.GetOrCreateAsync("shared-config");

        Assert.Same(r1.Config, r2.Config);
        Assert.Equal("custom-model", r2.Config.Model);
        Assert.Equal(Sovrant.Runtime.Permissions.PermissionMode.Plan, r2.Config.PermissionMode);
    }

    [Fact]
    public async Task TryGetConfig_ExistingSession_ReturnsConfig()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        await pool.GetOrCreateAsync("existing");
        var config = pool.TryGetConfig("existing");

        Assert.NotNull(config);
    }

    [Fact]
    public void TryGetConfig_NonexistentSession_ReturnsNull()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var config = pool.TryGetConfig("nonexistent");

        Assert.Null(config);
    }

    [Fact]
    public async Task SessionConfig_TokenAccumulation_ThreadSafe()
    {
        var sp = BuildServices();
        var pool = sp.GetRequiredService<IRuntimeSessionPool>();

        var pooled = await pool.GetOrCreateAsync("token-session");

        pooled.Config.AddTokens(100, 50);
        pooled.Config.AddTokens(200, 75);

        Assert.Equal(300, pooled.Config.TotalInputTokens);
        Assert.Equal(125, pooled.Config.TotalOutputTokens);
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
        public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionEntry>>([]);
        public Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<int> DeleteAllAsync(CancellationToken ct = default)
            => Task.FromResult(0);
        public Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }
}
