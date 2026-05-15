using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Preferences;

public sealed class SqliteUserPreferenceStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteUserPreferenceStore _store;

    public SqliteUserPreferenceStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_prefs_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteUserPreferenceStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Set_Then_Get_RoundTrips()
    {
        await _store.SetAsync("alice", UserPreferenceKeys.Model, "gpt-5");

        Assert.Equal("gpt-5", await _store.GetAsync("alice", UserPreferenceKeys.Model));
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        Assert.Null(await _store.GetAsync("alice", "no.such.key"));
    }

    [Fact]
    public async Task Set_OnConflict_Updates()
    {
        await _store.SetAsync("alice", UserPreferenceKeys.MaxTokens, "4096");
        await _store.SetAsync("alice", UserPreferenceKeys.MaxTokens, "8192");

        Assert.Equal("8192", await _store.GetAsync("alice", UserPreferenceKeys.MaxTokens));
    }

    [Fact]
    public async Task Users_AreIsolated()
    {
        await _store.SetAsync("alice", UserPreferenceKeys.Model, "gpt-5");
        await _store.SetAsync("bob", UserPreferenceKeys.Model, "claude-opus-4-7");

        Assert.Equal("gpt-5", await _store.GetAsync("alice", UserPreferenceKeys.Model));
        Assert.Equal("claude-opus-4-7", await _store.GetAsync("bob", UserPreferenceKeys.Model));
    }

    [Fact]
    public async Task Delete_RemovesRow_AndIsIdempotent()
    {
        await _store.SetAsync("alice", "k", "v");
        await _store.DeleteAsync("alice", "k");
        await _store.DeleteAsync("alice", "k"); // no-op

        Assert.Null(await _store.GetAsync("alice", "k"));
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyThatUsersRows()
    {
        await _store.SetAsync("alice", "k1", "v1");
        await _store.SetAsync("alice", "k2", "v2");
        await _store.SetAsync("bob", "k1", "bobs");

        var all = await _store.GetAllAsync("alice");

        Assert.Equal(2, all.Count);
        Assert.Equal("v1", all["k1"]);
        Assert.Equal("v2", all["k2"]);
    }
}
