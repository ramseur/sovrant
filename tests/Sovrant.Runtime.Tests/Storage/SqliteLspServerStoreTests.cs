using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteLspServerStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteLspServerStore _store;

    public SqliteLspServerStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_lsp_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteLspServerStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Upsert_Then_Get_RoundTripsCommandArgsEnv()
    {
        var entry = new LspServerEntry(
            "omnisharp",
            ["--languageserver", "--verbose"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["DOTNET_ROOT"] = "/usr/share/dotnet" });

        await _store.UpsertAsync("csharp", entry);

        var loaded = await _store.GetAsync("csharp");
        Assert.NotNull(loaded);
        Assert.Equal("omnisharp", loaded!.Command);
        Assert.Equal(2, loaded.Args.Count);
        Assert.Equal("/usr/share/dotnet", loaded.Env["DOTNET_ROOT"]);
    }

    [Fact]
    public async Task GetAsync_MissingLanguage_ReturnsNull()
    {
        Assert.Null(await _store.GetAsync("rust"));
    }

    [Fact]
    public async Task Upsert_Existing_OverwritesPreviousEntry()
    {
        await _store.UpsertAsync("python",
            new LspServerEntry("pylsp", [], new Dictionary<string, string>(StringComparer.Ordinal)));
        await _store.UpsertAsync("python",
            new LspServerEntry("pyright", ["--stdio"], new Dictionary<string, string>(StringComparer.Ordinal)));

        var loaded = await _store.GetAsync("python");
        Assert.Equal("pyright", loaded!.Command);
        Assert.Single(loaded.Args);
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        await _store.UpsertAsync("go",
            new LspServerEntry("gopls", [], new Dictionary<string, string>(StringComparer.Ordinal)));
        await _store.DeleteAsync("go");

        Assert.Null(await _store.GetAsync("go"));
    }

    [Fact]
    public async Task GetAll_ReturnsAllConfiguredLanguages()
    {
        await _store.UpsertAsync("ts",
            new LspServerEntry("typescript-language-server", [], new Dictionary<string, string>(StringComparer.Ordinal)));
        await _store.UpsertAsync("rust",
            new LspServerEntry("rust-analyzer", [], new Dictionary<string, string>(StringComparer.Ordinal)));

        var all = await _store.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.True(all.ContainsKey("ts"));
        Assert.True(all.ContainsKey("rust"));
    }
}
