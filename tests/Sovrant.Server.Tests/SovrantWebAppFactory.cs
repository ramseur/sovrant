using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set required env vars before the host builds.
        Environment.SetEnvironmentVariable("SOVRANT_TOKEN", "test-token-123");
        Environment.SetEnvironmentVariable("LLM_API_KEY", "fake-key");
        Environment.SetEnvironmentVariable("LLM_BASE_URL", "https://api.example.com/v1");

        builder.ConfigureServices(services =>
        {
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
}

/// <summary>In-memory session store for tests.</summary>
public sealed class FakeSessionStore : ISessionStore
{
    private readonly Dictionary<string, List<SessionEntry>> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public void Seed(string sessionId, params SessionEntry[] entries)
    {
        _sessions[sessionId] = [.. entries];
    }

    public Task AppendAsync(string sessionId, SessionEntry entry, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var list))
        {
            list = [];
            _sessions[sessionId] = list;
        }
        list.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var list))
            return Task.FromResult<IReadOnlyList<SessionEntry>>(list);
        return Task.FromResult<IReadOnlyList<SessionEntry>>([]);
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_sessions.Keys.ToList());
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

/// <summary>Fake conversation runtime that yields a canned response.</summary>
public sealed class FakeConversationRuntime : IConversationRuntime
{
    public string SessionId => "fake-session";
    public string NextResponse { get; set; } = "Hello from fake runtime.";

    public Task InitializeSessionAsync(string? sessionId, CancellationToken ct = default) =>
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
