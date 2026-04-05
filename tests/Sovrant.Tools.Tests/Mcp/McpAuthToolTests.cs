using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Tools.Mcp;

namespace Sovrant.Tools.Tests.Mcp;

/// <summary>Tests for <see cref="McpAuthTool"/>.</summary>
public sealed class McpAuthToolTests
{
    private static McpOAuthService CreateOAuthService(SovrantConfig config) =>
        new(config, new InMemoryCredentialStore(), null!, NullLogger<McpOAuthService>.Instance);

    private static SovrantConfig ConfigWithOAuth(string serverName = "github") =>
        new()
        {
            McpServers = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal)
            {
                [serverName] = new McpServerConfig
                {
                    Command = "echo",
                    OAuthConfig = new McpOAuthConfig
                    {
                        ClientId = "cid",
                        AuthorizationUrl = new Uri("https://auth.example.com/oauth/authorize"),
                        TokenUrl = new Uri("https://auth.example.com/oauth/token"),
                        RedirectUri = new Uri("http://localhost:5200/v1/mcp/auth/callback"),
                    },
                },
            },
        };

    // ── Definition ────────────────────────────────────────────────────────────

    [Fact]
    public void Definition_Name_IsMcpAuth()
    {
        using var oauthSvc = CreateOAuthService(ConfigWithOAuth());
        var tool = new McpAuthTool(oauthSvc);
        Assert.Equal("McpAuth", tool.Definition.Name);
    }

    [Fact]
    public void Definition_Description_MentionsOAuth()
    {
        using var oauthSvc = CreateOAuthService(ConfigWithOAuth());
        var tool = new McpAuthTool(oauthSvc);
        Assert.Contains("OAuth", tool.Definition.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Definition_Schema_RequiresServerParameter()
    {
        using var oauthSvc = CreateOAuthService(ConfigWithOAuth());
        var tool = new McpAuthTool(oauthSvc);
        var schema = tool.Definition.InputSchema;
        var required = schema.GetProperty("required");
        var names = required.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("server", names);
    }

    // ── ExecuteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_MissingServerParam_ReturnsHelpMessage()
    {
        using var oauthSvc = CreateOAuthService(ConfigWithOAuth());
        var tool = new McpAuthTool(oauthSvc);
        var input = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(input);
        Assert.Contains("server", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_KnownServer_ReturnsAuthUrl()
    {
        using var oauthSvc = CreateOAuthService(ConfigWithOAuth("github"));
        var tool = new McpAuthTool(oauthSvc);
        var input = JsonDocument.Parse("""{"server":"github"}""").RootElement;
        var result = await tool.ExecuteAsync(input);
        Assert.Contains("https://auth.example.com/oauth/authorize", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_KnownServer_ResultMentionsServerName()
    {
        using var oauthSvc = CreateOAuthService(ConfigWithOAuth("github"));
        var tool = new McpAuthTool(oauthSvc);
        var input = JsonDocument.Parse("""{"server":"github"}""").RootElement;
        var result = await tool.ExecuteAsync(input);
        Assert.Contains("github", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_UnknownServer_ReturnsErrorMessage()
    {
        using var oauthSvc = CreateOAuthService(ConfigWithOAuth());
        var tool = new McpAuthTool(oauthSvc);
        var input = JsonDocument.Parse("""{"server":"no-such-server"}""").RootElement;
        var result = await tool.ExecuteAsync(input);
        Assert.Contains("not configured", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_ServerWithNoOAuth_ReturnsErrorMessage()
    {
        var config = new SovrantConfig
        {
            McpServers = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal)
            {
                ["bare"] = new McpServerConfig { Command = "echo" },
            },
        };
        using var oauthSvc = CreateOAuthService(config);
        var tool = new McpAuthTool(oauthSvc);
        var input = JsonDocument.Parse("""{"server":"bare"}""").RootElement;
        var result = await tool.ExecuteAsync(input);
        Assert.Contains("OAuth", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
