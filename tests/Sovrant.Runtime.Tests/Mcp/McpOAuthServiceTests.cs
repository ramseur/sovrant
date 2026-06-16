using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>Tests for <see cref="McpOAuthService"/> — no network calls, no real MCP processes.</summary>
public sealed class McpOAuthServiceTests
{
    private static InMemoryMcpServerStore StoreWithOAuth(
        string serverName = "github",
        string? tokenEnvVar = "GITHUB_TOKEN")
    {
        var store = new InMemoryMcpServerStore();
        store.Servers[serverName] = new McpServerConfig
        {
            Command = "echo",
            OAuthConfig = new McpOAuthConfig
            {
                ClientId = "test-client-id",
                AuthorizationUrl = new Uri("https://github.com/login/oauth/authorize"),
                TokenUrl = new Uri("https://github.com/login/oauth/access_token"),
                Scopes = ["repo", "read:org"],
                TokenEnvVar = tokenEnvVar ?? string.Empty,
                RedirectUri = new Uri("http://localhost:5200/v1/mcp/auth/callback"),
            },
        };
        return store;
    }

    private static McpOAuthService CreateService(IMcpServerStore? store = null) =>
        new(
            store ?? StoreWithOAuth(),
            new InMemoryCredentialStore(),
            null!,   // McpToolRegistrar — not needed for URL-generation tests
            new StubHttpClientFactory(),
            NullLogger<McpOAuthService>.Instance);

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    // ── GenerateAuthorizationUrlAsync ─────────────────────────────────────────

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsResponseTypeCode()
    {
        var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("response_type=code", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsClientId()
    {
        var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("client_id=test-client-id", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsRedirectUri()
    {
        var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("redirect_uri=", url, StringComparison.Ordinal);
        Assert.Contains("v1%2Fmcp%2Fauth%2Fcallback", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsStateParameter()
    {
        var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("state=", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsPkceParameters()
    {
        var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("code_challenge=", url, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsScopes()
    {
        var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("scope=", url, StringComparison.Ordinal);
        Assert.Contains("repo", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_TwoCallsProduceDifferentStates()
    {
        var svc = CreateService();
        var url1 = await svc.GenerateAuthorizationUrlAsync("github");
        var url2 = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.NotEqual(url1, url2);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_UnknownServer_Throws()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GenerateAuthorizationUrlAsync("unknown-server"));
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_NoOAuthConfig_Throws()
    {
        var store = new InMemoryMcpServerStore();
        store.Servers["bare"] = new McpServerConfig { Command = "echo" };
        var svc = CreateService(store);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.GenerateAuthorizationUrlAsync("bare"));
    }

    // ── ExchangeCodeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExchangeCodeAsync_UnknownState_Throws()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ExchangeCodeAsync("unknown-state-xyz", "any-code"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>In-memory IMcpServerStore for tests.</summary>
    private sealed class InMemoryMcpServerStore : IMcpServerStore
    {
        public Dictionary<string, McpServerConfig> Servers { get; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyDictionary<string, McpServerConfig>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, McpServerConfig>>(Servers);

        public Task<IReadOnlyList<McpServerEntry>> GetAllEntriesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<McpServerEntry>>(
                Servers.Select((kv, i) => new McpServerEntry(i.ToString(), kv.Key, kv.Value)).ToList());

        public Task<McpServerConfig?> GetAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(Servers.TryGetValue(name, out var c) ? c : null);

        public Task<McpServerEntry?> GetEntryAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(Servers.TryGetValue(name, out var c) ? new McpServerEntry(name, name, c) : null);

        public Task UpsertAsync(string name, McpServerConfig config, CancellationToken ct = default)
        {
            Servers[name] = config;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string name, CancellationToken ct = default)
        {
            Servers.Remove(name);
            return Task.CompletedTask;
        }
    }

    /// <summary>In-memory ICredentialStore for tests — never touches disk.</summary>
    private sealed class InMemoryCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.Ordinal);

        public Task StoreAsync(string key, string value, CancellationToken ct = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> RetrieveAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }
}
