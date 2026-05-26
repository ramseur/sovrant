using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Storage;

namespace Sovrant.Server.Tests;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> for Sovrant server integration tests.
/// Overrides external dependencies (LLM providers, session store) with in-memory fakes
/// so tests run without network access or filesystem side-effects.
/// </summary>
public sealed class SovrantWebAppFactory : WebApplicationFactory<Program>
{
    /// <summary>The fake session store shared across all tests using this factory.</summary>
    public FakeSessionStore SessionStore { get; } = new();

    /// <summary>The fake router shared across all tests using this factory.</summary>
    public FakeSmartRouter Router { get; } = new();

    /// <summary>The fake runtime shared across all tests using this factory.</summary>
    public FakeConversationRuntime Runtime { get; } = new();

    private readonly object _tokenLock = new();
    private volatile string? _testAdminToken;

    /// <summary>
    /// A valid <c>svt_</c> bearer token for the seeded test admin user.
    /// Lazily issued on first access. The seed user is created on demand because
    /// neither <see cref="SqliteStorageProvider"/> nor <c>Sovrant.Server.Program</c>
    /// seed one — that is the Web frontend's responsibility in production.
    /// All tests in the same fixture share one token. Thread-safe via double-checked locking.
    /// </summary>
    public string TestAdminToken
    {
        get
        {
            if (_testAdminToken is not null) return _testAdminToken;
            lock (_tokenLock)
            {
                if (_testAdminToken is not null) return _testAdminToken;
                var seedUserId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName;

                // Ensure the admin user row exists. TokenService.IssueAsync has a
                // FK to users(user_id); without this, fresh-DB test fixtures fail.
                var users = Server.Services.GetRequiredService<Sovrant.Runtime.Users.IUserService>();
                var existing = users.GetAsync(seedUserId).GetAwaiter().GetResult();
                if (existing is null)
                {
                    users.CreateAsync(
                        username: seedUserId,
                        email: null,
                        role: "admin",
                        team: null,
                        userId: seedUserId).GetAwaiter().GetResult();
                }

                var tokens = Server.Services.GetRequiredService<ITokenService>();
                var issued = tokens.IssueAsync(seedUserId, name: "test-admin", expiresAt: DateTimeOffset.UtcNow.AddDays(30))
                    .GetAwaiter().GetResult();
                _testAdminToken = issued.Plaintext;
            }
            return _testAdminToken!;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set required env vars before the host builds.
        Environment.SetEnvironmentVariable("LLM_API_KEY", "fake-key");
        Environment.SetEnvironmentVariable("LLM_BASE_URL", "https://api.example.com/v1");

        // Use a unique named in-memory DB per factory instance so tests are isolated
        // but all connections within one test share the same in-memory DB via Cache=Shared.
        var testDbName = $"file:sovrant_test_{Guid.NewGuid():N}?mode=memory&cache=shared";

        builder.ConfigureServices(services =>
        {
            // Replace only the concrete SqliteStorageProvider with a test-isolated instance.
            // The IStorageProvider and ISqliteConnectionFactory forward-registrations from
            // AddSovrantRuntime will resolve against this replacement automatically.
            services.RemoveAll(typeof(SqliteStorageProvider));
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<SqliteStorageProvider>>();
                return new SqliteStorageProvider(logger, dbPath: testDbName);
            });

            // Use no-op audit store (avoids needing DB tables for audit in simple tests).
            services.RemoveAll(typeof(IAuditStore));
            services.AddSingleton<IAuditStore>(new FakeAuditStore());

            // Replace ISessionStore with in-memory fake.
            Replace<ISessionStore>(services, SessionStore);

            // Replace ISmartRouter with fake.
            Replace<ISmartRouter>(services, Router);

            // Replace IConversationRuntime with fake (transient still works — same instance).
            Replace<IConversationRuntime>(services, Runtime);
        });
    }

    private static void Replace<T>(IServiceCollection services, T implementation) where T : class
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (existing is not null)
            services.Remove(existing);
        services.AddSingleton(typeof(T), implementation);
    }

    private static void ReplaceFactory<T>(IServiceCollection services, Func<IServiceProvider, T> factory) where T : class
    {
        // Remove all registrations for the service type (concrete + interface).
        services.RemoveAll(typeof(T));
        services.RemoveAll(typeof(SqliteStorageProvider));
        services.AddSingleton(typeof(T), sp => factory(sp)!);
    }
}

/// <summary>
/// In-memory session store for tests. Honors Phase 38 ownership semantics so
/// cross-user isolation tests can use it in place of SQLite.
/// </summary>
public sealed class FakeSessionStore : ISessionStore
{
    private readonly Dictionary<string, List<SessionEntry>> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _owners = new(StringComparer.OrdinalIgnoreCase);

    public void Seed(string sessionId, string? ownerUserId, params SessionEntry[] entries)
    {
        _sessions[sessionId] = [.. entries];
        _owners[sessionId] = ownerUserId;
    }

    public void Seed(string sessionId, params SessionEntry[] entries) =>
        Seed(sessionId, ownerUserId: null, entries);

    public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var list))
        {
            list = [];
            _sessions[sessionId] = list;
            _owners[sessionId] = ownerUserId;
        }
        list.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var list))
            return Task.FromResult<IReadOnlyList<SessionEntry>>([]);
        if (ownerUserId is not null)
        {
            _owners.TryGetValue(sessionId, out var owner);
            if (owner is null || !string.Equals(owner, ownerUserId, StringComparison.Ordinal))
                return Task.FromResult<IReadOnlyList<SessionEntry>>([]);
        }
        return Task.FromResult<IReadOnlyList<SessionEntry>>(list);
    }

    public Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default)
    {
        if (ownerUserId is null)
            return Task.FromResult<IReadOnlyList<string>>(_sessions.Keys.ToList());
        var filtered = _sessions.Keys
            .Where(k => _owners.TryGetValue(k, out var o) && string.Equals(o, ownerUserId, StringComparison.Ordinal))
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(filtered);
    }

    public Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
    {
        if (!_sessions.ContainsKey(sessionId))
            return Task.FromResult(false);
        if (ownerUserId is not null)
        {
            _owners.TryGetValue(sessionId, out var owner);
            if (owner is null || !string.Equals(owner, ownerUserId, StringComparison.Ordinal))
                return Task.FromResult(false);
        }
        _sessions.Remove(sessionId);
        _owners.Remove(sessionId);
        return Task.FromResult(true);
    }

    public Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        var count = _sessions.Count;
        _sessions.Clear();
        _owners.Clear();
        return Task.FromResult(count);
    }

    public Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default)
    {
        _owners.TryGetValue(sessionId, out var owner);
        return Task.FromResult(owner);
    }

    private readonly Dictionary<string, string> _titles = new(StringComparer.OrdinalIgnoreCase);

    public Task SetTitleAsync(string sessionId, string title, string? ownerUserId = null, CancellationToken ct = default)
    {
        _titles[sessionId] = title;
        return Task.CompletedTask;
    }

    public Task<string?> GetTitleAsync(string sessionId, CancellationToken ct = default)
    {
        _titles.TryGetValue(sessionId, out var title);
        return Task.FromResult<string?>(title);
    }

    public Task<IReadOnlyList<SessionListItem>> ListWithTitlesAsync(string? ownerUserId = null, CancellationToken ct = default)
    {
        IEnumerable<string> keys = _sessions.Keys;
        if (ownerUserId is not null)
            keys = keys.Where(k => _owners.TryGetValue(k, out var o) && string.Equals(o, ownerUserId, StringComparison.Ordinal));
        var result = keys
            .Select(id => new SessionListItem(id, _titles.GetValueOrDefault(id), DateTimeOffset.UtcNow))
            .ToList();
        return Task.FromResult<IReadOnlyList<SessionListItem>>(result);
    }

    public Task<IReadOnlyList<SessionListItem>> SearchAsync(string query, string? ownerUserId = null, int limit = 50, CancellationToken ct = default)
    {
        // Simple in-memory substring search for tests.
        var matches = _sessions
            .Where(kv => ownerUserId is null || (_owners.TryGetValue(kv.Key, out var o) && string.Equals(o, ownerUserId, StringComparison.Ordinal)))
            .Where(kv => kv.Value.Any(e => e.Content.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Take(limit)
            .Select(kv => new SessionListItem(kv.Key, _titles.GetValueOrDefault(kv.Key), DateTimeOffset.UtcNow))
            .ToList();
        return Task.FromResult<IReadOnlyList<SessionListItem>>(matches);
    }

    private readonly Dictionary<string, IReadOnlyList<string>?> _mcpConnections = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<string>?> GetMcpConnectionsAsync(string sessionId, CancellationToken ct = default)
        => Task.FromResult(_mcpConnections.TryGetValue(sessionId, out var v) ? v : null);

    public Task SetMcpConnectionsAsync(string sessionId, IReadOnlyList<string>? servers, string? ownerUserId = null, CancellationToken ct = default)
    {
        _mcpConnections[sessionId] = servers;
        return Task.CompletedTask;
    }

    private readonly Dictionary<string, bool> _privacy = new(StringComparer.OrdinalIgnoreCase);

    public Task UpdatePrivacyAsync(string sessionId, string ownerUserId, bool isPrivate, CancellationToken ct = default)
    {
        if (!_owners.TryGetValue(sessionId, out var owner) || !string.Equals(owner, ownerUserId, StringComparison.Ordinal))
            return Task.CompletedTask;
        _privacy[sessionId] = isPrivate;
        return Task.CompletedTask;
    }

    public Task<bool?> GetIsPrivateAsync(string sessionId, CancellationToken ct = default)
    {
        if (_privacy.TryGetValue(sessionId, out var v)) return Task.FromResult<bool?>(v);
        return Task.FromResult<bool?>(null);
    }
}

/// <summary>Fake smart router that returns a canned provider list.</summary>
public sealed class FakeSmartRouter : ISmartRouter
{
    public List<ProviderStatus> Providers { get; set; } =
    [
        new("test-provider", Healthy: true, LatencyMs: 10.0, CostPer1kTokens: 0.01,
            RequestCount: 0, ErrorCount: 0, ErrorRate: "0.0%", Score: "0.010"),
    ];

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) =>
        Task.FromResult<ILlmProvider>(new FakeLlmProvider());

    public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) =>
        Task.CompletedTask;

    public IReadOnlyList<ProviderStatus> GetStatus() => Providers;

    public Task PinProviderAsync(string? providerName, CancellationToken ct = default)
    {
        if (providerName is not null && !Providers.Any(p => string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Provider '{providerName}' is not configured.");
        return Task.CompletedTask;
    }

    public Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default) =>
        Task.FromResult(new RoutingDecision(new FakeLlmProvider(), null, null, null));
    public bool IntentRoutingEnabled { get; set; }
}

/// <summary>Fake LLM provider — never called in route-level tests.</summary>
public sealed class FakeLlmProvider : ILlmProvider
{
    public string Name => "test-provider";
    public Uri BaseUrl => new("https://api.example.com/v1");

    public Task<Api.Result<MessageResponse>> SendAsync(MessagesRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException("Tests should not reach the LLM provider.");

    public IAsyncEnumerable<StreamEvent> StreamAsync(MessagesRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException("Tests should not reach the LLM provider.");
}

/// <summary>No-op audit store for server tests.</summary>
public sealed class FakeAuditStore : IAuditStore
{
    public readonly List<(string UserId, string Kind, string Id, bool IsPrivate)> PrivacyLog = [];

    public Task LogGovernanceEventAsync(GovernanceContext context, GovernanceVerdict verdict, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task LogBashCommandAsync(string command, string? sessionId, int exitCode, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task LogPrivacyChangeAsync(string userId, string entityKind, string entityId, bool newIsPrivate, CancellationToken ct = default)
    {
        PrivacyLog.Add((userId, entityKind, entityId, newIsPrivate));
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Fake conversation runtime that yields a canned response.</summary>
public sealed class FakeConversationRuntime : IConversationRuntime
{
    public string SessionId => "fake-session";
    public string NextResponse { get; set; } = "Hello from fake runtime.";

    public Task InitializeSessionAsync(string? sessionId, string? ownerUserId = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    public async IAsyncEnumerable<RuntimeEvent> RunTurnAsync(
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new RuntimeEvent.TextChunk(NextResponse);
        yield return new RuntimeEvent.TurnComplete(null, 10, 20);
        await Task.CompletedTask;
    }

    public void Reset() { }
}
