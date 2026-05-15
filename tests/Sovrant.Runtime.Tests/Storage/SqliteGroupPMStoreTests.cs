using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteGroupPMStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IGroupPMStore _store;

    public SqliteGroupPMStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_pm_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteGroupPMStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private static GroupPMAssignment MakeAssignment(
        string groupId = "group-a",
        string groupType = "team",
        string pmTemplate = "pm-coordinator",
        string workspaceId = "ws-1",
        string? projectId = null) =>
        new(groupId, groupType, pmTemplate, workspaceId, projectId, string.Empty);

    [Fact]
    public async Task UpsertAndGet_RoundTrips()
    {
        await _store.UpsertAsync(MakeAssignment());

        var result = await _store.GetAsync("group-a");
        Assert.NotNull(result);
        Assert.Equal("group-a", result.GroupId);
        Assert.Equal("team", result.GroupType);
        Assert.Equal("pm-coordinator", result.PMTemplate);
        Assert.Equal("ws-1", result.WorkspaceId);
    }

    [Fact]
    public async Task Get_UnknownGroup_ReturnsNull()
    {
        var result = await _store.GetAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task Upsert_UpdatesExisting()
    {
        await _store.UpsertAsync(MakeAssignment());
        await _store.UpsertAsync(MakeAssignment(pmTemplate: "custom-pm"));

        var result = await _store.GetAsync("group-a");
        Assert.NotNull(result);
        Assert.Equal("custom-pm", result.PMTemplate);
    }

    [Fact]
    public async Task ListByWorkspace_ReturnsOnlyMatchingWorkspace()
    {
        await _store.UpsertAsync(MakeAssignment(groupId: "g1", workspaceId: "ws-1"));
        await _store.UpsertAsync(MakeAssignment(groupId: "g2", workspaceId: "ws-1"));
        await _store.UpsertAsync(MakeAssignment(groupId: "g3", workspaceId: "ws-2"));

        var ws1 = await _store.ListByWorkspaceAsync("ws-1");
        Assert.Equal(2, ws1.Count);

        var ws2 = await _store.ListByWorkspaceAsync("ws-2");
        Assert.Single(ws2);
    }

    [Fact]
    public async Task Delete_RemovesAssignment()
    {
        await _store.UpsertAsync(MakeAssignment());

        await _store.DeleteAsync("group-a");

        var result = await _store.GetAsync("group-a");
        Assert.Null(result);
    }
}
