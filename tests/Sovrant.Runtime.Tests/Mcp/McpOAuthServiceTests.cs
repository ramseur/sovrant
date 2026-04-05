using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>Tests for <see cref="McpOAuthService"/> — no network calls, no real MCP processes.</summary>
public sealed class McpOAuthServiceTests
{
    private static SovrantConfig ConfigWithOAuth(
        string serverName = "github",
        string? tokenEnvVar = "GITHUB_TOKEN") =>
        new()
        {
            McpServers = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal)
            {
                [serverName] = new McpServerConfig
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
                },
            },
        };

    private static McpOAuthService CreateService(SovrantConfig? config = null) =>
        new(
            config ?? ConfigWithOAuth(),
            new InMemoryCredentialStore(),
            null!,   // McpToolRegistrar — not needed for URL-generation tests
            NullLogger<McpOAuthService>.Instance);

    // ── GenerateAuthorizationUrlAsync ─────────────────────────────────────────

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsResponseTypeCode()
    {
        using var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("response_type=code", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsClientId()
    {
        using var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("client_id=test-client-id", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsRedirectUri()
    {
        using var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("redirect_uri=", url, StringComparison.Ordinal);
        Assert.Contains("v1%2Fmcp%2Fauth%2Fcallback", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsStateParameter()
    {
        using var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("state=", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsPkceParameters()
    {
        using var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("code_challenge=", url, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_ContainsScopes()
    {
        using var svc = CreateService();
        var url = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.Contains("scope=", url, StringComparison.Ordinal);
        Assert.Contains("repo", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_TwoCallsProduceDifferentStates()
    {
        using var svc = CreateService();
        var url1 = await svc.GenerateAuthorizationUrlAsync("github");
        var url2 = await svc.GenerateAuthorizationUrlAsync("github");
        Assert.NotEqual(url1, url2);
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_UnknownServer_Throws()
    {
        using var svc = CreateService();
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GenerateAuthorizationUrlAsync("unknown-server"));
    }

    [Fact]
    public async Task GenerateAuthorizationUrl_NoOAuthConfig_Throws()
    {
        var config = new SovrantConfig
        {
            McpServers = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal)
            {
                ["bare"] = new McpServerConfig { Command = "echo" },
            },
        };
        using var svc = CreateService(config);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.GenerateAuthorizationUrlAsync("bare"));
    }

    // ── ExchangeCodeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExchangeCodeAsync_UnknownState_Throws()
    {
        using var svc = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ExchangeCodeAsync("unknown-state-xyz", "any-code"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
