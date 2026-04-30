using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteWorkspaceSettingsStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteWorkspaceSettingsStore _store;

    public SqliteWorkspaceSettingsStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_settings_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkspaceSettingsStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Set_Then_Get_RoundTrips()
    {
        await _store.SetAsync("ws-1", WorkspaceSettingsKeys.SessionBudgetUsd, "12.50");

        Assert.Equal("12.50",
            await _store.GetAsync("ws-1", WorkspaceSettingsKeys.SessionBudgetUsd));
    }

    [Fact]
    public async Task Get_FallsBackToGlobalRow_WhenWorkspaceMissing()
    {
        await _store.SetAsync(
            WorkspaceSettingsKeys.GlobalWorkspaceId,
            WorkspaceSettingsKeys.SessionTtlSeconds,
            "1800");

        var got = await _store.GetAsync("ws-no-row", WorkspaceSettingsKeys.SessionTtlSeconds);
        Assert.Equal("1800", got);
    }

    [Fact]
    public async Task Workspace_OverridesGlobalRow()
    {
        await _store.SetAsync(
            WorkspaceSettingsKeys.GlobalWorkspaceId,
            WorkspaceSettingsKeys.MaxSessions,
            "100");
        await _store.SetAsync("ws-busy", WorkspaceSettingsKeys.MaxSessions, "1000");

        Assert.Equal("100", await _store.GetGlobalAsync(WorkspaceSettingsKeys.MaxSessions));
        Assert.Equal("1000", await _store.GetAsync("ws-busy", WorkspaceSettingsKeys.MaxSessions));
        Assert.Equal("100", await _store.GetAsync("ws-other", WorkspaceSettingsKeys.MaxSessions));
    }

    [Fact]
    public async Task Set_OnConflict_Updates()
    {
        await _store.SetAsync("ws-1", "k", "first");
        await _store.SetAsync("ws-1", "k", "second");

        Assert.Equal("second", await _store.GetAsync("ws-1", "k"));
    }

    [Fact]
    public async Task Delete_RemovesRow()
    {
        await _store.SetAsync("ws-1", "k", "v");
        await _store.DeleteAsync("ws-1", "k");

        Assert.Null(await _store.GetAsync("ws-1", "k"));
    }

    [Fact]
    public async Task GetAll_MergesGlobalAndWorkspace_WorkspaceWins()
    {
        await _store.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, "a", "global-a");
        await _store.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, "b", "global-b");
        await _store.SetAsync("ws-1", "b", "ws-b");
        await _store.SetAsync("ws-1", "c", "ws-c");

        var all = await _store.GetAllAsync("ws-1");

        Assert.Equal("global-a", all["a"]);
        Assert.Equal("ws-b", all["b"]);
        Assert.Equal("ws-c", all["c"]);
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        Assert.Null(await _store.GetAsync("ws-none", "missing.key"));
    }
}
