using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Hooks;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteHookStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteHookStore _store;

    public SqliteHookStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_hooks_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteHookStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Upsert_RoundTripsAllFields()
    {
        var hook = new HookConfig(
            Event: HookEvent.PostToolUse,
            Command: "lint {file}",
            Matcher: new HookMatcher(Tool: "Edit", Glob: "*.cs"),
            TimeoutMs: 5000,
            Id: "hook-1",
            Enabled: true);

        await _store.UpsertAsync(hook);

        var enabled = await _store.GetEnabledAsync();
        var got = Assert.Single(enabled);
        Assert.Equal("hook-1", got.Id);
        Assert.Equal(HookEvent.PostToolUse, got.Event);
        Assert.Equal("lint {file}", got.Command);
        Assert.NotNull(got.Matcher);
        Assert.Equal("Edit", got.Matcher.Tool);
        Assert.Equal("*.cs", got.Matcher.Glob);
        Assert.Equal(5000, got.TimeoutMs);
        Assert.True(got.Enabled);
    }

    [Fact]
    public async Task GetEnabled_FiltersDisabled()
    {
        await _store.UpsertAsync(new HookConfig(HookEvent.Stop, "alpha", Id: "h-on", Enabled: true));
        await _store.UpsertAsync(new HookConfig(HookEvent.Stop, "beta", Id: "h-off", Enabled: false));

        var enabled = await _store.GetEnabledAsync();
        var all = await _store.GetAllAsync();

        Assert.Single(enabled);
        Assert.Equal("h-on", enabled[0].Id);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Upsert_OnConflict_Updates()
    {
        await _store.UpsertAsync(new HookConfig(HookEvent.Stop, "first", Id: "same"));
        await _store.UpsertAsync(new HookConfig(HookEvent.Stop, "second", Id: "same"));

        var all = await _store.GetAllAsync();
        var got = Assert.Single(all);
        Assert.Equal("second", got.Command);
    }

    [Fact]
    public async Task Delete_RemovesRow()
    {
        await _store.UpsertAsync(new HookConfig(HookEvent.Stop, "x", Id: "h1"));
        await _store.DeleteAsync("h1");

        Assert.Empty(await _store.GetAllAsync());
    }

    [Fact]
    public async Task NullMatcher_DoesNotPersistTool_OrGlob()
    {
        await _store.UpsertAsync(new HookConfig(HookEvent.SessionStart, "boot", Id: "h"));
        var got = Assert.Single(await _store.GetEnabledAsync());
        Assert.Null(got.Matcher);
    }

    [Fact]
    public async Task Upsert_WithEmptyId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store.UpsertAsync(new HookConfig(HookEvent.Stop, "x", Id: "")));
    }
}
