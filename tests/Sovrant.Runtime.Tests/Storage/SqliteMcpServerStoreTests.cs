using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteMcpServerStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMcpServerStore _store;

    public SqliteMcpServerStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_mcp_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMcpServerStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Upsert_Then_Get_RoundTripsCommandArgsEnv()
    {
        var entry = new McpServerConfig
        {
            Command = "npx",
            Args = ["-y", "@modelcontextprotocol/server-filesystem", "/tmp"],
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["LOG_LEVEL"] = "debug" },
        };

        await _store.UpsertAsync("filesystem", entry);

        var loaded = await _store.GetAsync("filesystem");
        Assert.NotNull(loaded);
        Assert.Equal("npx", loaded!.Command);
        Assert.Equal(3, loaded.Args.Count);
        Assert.Equal("@modelcontextprotocol/server-filesystem", loaded.Args[1]);
        Assert.Equal("debug", loaded.Env["LOG_LEVEL"]);
    }

    [Fact]
    public async Task Upsert_Then_Get_PreservesOAuthMetadataMinusSecret()
    {
        var entry = new McpServerConfig
        {
            Command = "echo",
            Args = [],
            Env = new Dictionary<string, string>(StringComparer.Ordinal),
            OAuthConfig = new McpOAuthConfig
            {
                ClientId = "client-123",
                AuthorizationUrl = new Uri("https://auth.example.com/authorize"),
                TokenUrl = new Uri("https://auth.example.com/token"),
                Scopes = ["read", "write"],
                TokenEnvVar = "GITHUB_TOKEN",
            },
        };

        await _store.UpsertAsync("github", entry);

        var loaded = await _store.GetAsync("github");
        Assert.NotNull(loaded?.OAuthConfig);
        Assert.Equal("client-123", loaded.OAuthConfig!.ClientId);
        Assert.Equal("GITHUB_TOKEN", loaded.OAuthConfig.TokenEnvVar);
        Assert.Equal(2, loaded.OAuthConfig.Scopes.Count);
    }

    [Fact]
    public async Task GetAsync_MissingName_ReturnsNull()
    {
        Assert.Null(await _store.GetAsync("nope"));
    }

    [Fact]
    public async Task Upsert_Existing_OverwritesPreviousEntry()
    {
        await _store.UpsertAsync("svc", new McpServerConfig
        {
            Command = "old",
            Args = ["a"],
            Env = new Dictionary<string, string>(StringComparer.Ordinal),
        });
        await _store.UpsertAsync("svc", new McpServerConfig
        {
            Command = "new",
            Args = ["b", "c"],
            Env = new Dictionary<string, string>(StringComparer.Ordinal),
        });

        var loaded = await _store.GetAsync("svc");
        Assert.Equal("new", loaded!.Command);
        Assert.Equal(2, loaded.Args.Count);
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        await _store.UpsertAsync("temp", new McpServerConfig
        {
            Command = "x",
            Args = [],
            Env = new Dictionary<string, string>(StringComparer.Ordinal),
        });
        await _store.DeleteAsync("temp");

        Assert.Null(await _store.GetAsync("temp"));
    }

    [Fact]
    public async Task GetAll_ReturnsAllConfiguredServers()
    {
        await _store.UpsertAsync("a", new McpServerConfig { Command = "ca", Args = [], Env = new Dictionary<string, string>(StringComparer.Ordinal) });
        await _store.UpsertAsync("b", new McpServerConfig { Command = "cb", Args = [], Env = new Dictionary<string, string>(StringComparer.Ordinal) });

        var all = await _store.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal("ca", all["a"].Command);
        Assert.Equal("cb", all["b"].Command);
    }
}
