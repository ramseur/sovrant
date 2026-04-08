using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteSwarmEventStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ISwarmEventStore _store;

    public SqliteSwarmEventStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_swarm_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteSwarmEventStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private static SwarmEventRecord MakeRecord(
        string swarmId,
        string eventType = "PlanCreated",
        string payload = """{"hello":"world"}""",
        string? workspaceId = null,
        string? projectId = null,
        string? agentId = null) =>
        new(
            SwarmId: swarmId,
            EventType: eventType,
            Payload: payload,
            Timestamp: DateTimeOffset.UtcNow,
            AgentId: agentId,
            WorkspaceId: workspaceId,
            ProjectId: projectId);

    [Fact]
    public async Task RecordAndLoad_RoundTripsAllFields()
    {
        var record = MakeRecord(
            swarmId: "s1",
            eventType: "TaskCompleted",
            payload: """{"taskId":"t1","output":"done"}""",
            workspaceId: "ws-personal-alice",
            projectId: "proj-1",
            agentId: "coder");

        await _store.RecordEventAsync(record);

        var loaded = await _store.LoadEventsAsync("s1");

        var single = Assert.Single(loaded);
        Assert.Equal("s1", single.SwarmId);
        Assert.Equal("TaskCompleted", single.EventType);
        Assert.Equal("""{"taskId":"t1","output":"done"}""", single.Payload);
        Assert.Equal("ws-personal-alice", single.WorkspaceId);
        Assert.Equal("proj-1", single.ProjectId);
        Assert.Equal("coder", single.AgentId);
    }

    [Fact]
    public async Task LoadEvents_ReturnsInsertionOrder()
    {
        await _store.RecordEventAsync(MakeRecord("s1", eventType: "PlanCreated"));
        await _store.RecordEventAsync(MakeRecord("s1", eventType: "TaskStarted"));
        await _store.RecordEventAsync(MakeRecord("s1", eventType: "TaskCompleted"));

        var events = await _store.LoadEventsAsync("s1");

        Assert.Equal(3, events.Count);
        Assert.Equal("PlanCreated", events[0].EventType);
        Assert.Equal("TaskStarted", events[1].EventType);
        Assert.Equal("TaskCompleted", events[2].EventType);
    }

    [Fact]
    public async Task LoadEvents_UnknownSwarm_ReturnsEmpty()
    {
        var events = await _store.LoadEventsAsync("nonexistent");
        Assert.Empty(events);
    }

    [Fact]
    public async Task ListSwarms_ReturnsDistinctIds()
    {
        await _store.RecordEventAsync(MakeRecord("s1"));
        await _store.RecordEventAsync(MakeRecord("s1"));
        await _store.RecordEventAsync(MakeRecord("s2"));

        var ids = await _store.ListSwarmsAsync();
        Assert.Equal(2, ids.Count);
        Assert.Contains("s1", ids);
        Assert.Contains("s2", ids);
    }

    [Fact]
    public async Task ListSwarms_FilterByWorkspace_OnlyMatchingRows()
    {
        await _store.RecordEventAsync(MakeRecord("s-alice", workspaceId: "ws-alice"));
        await _store.RecordEventAsync(MakeRecord("s-bob", workspaceId: "ws-bob"));
        await _store.RecordEventAsync(MakeRecord("s-unscoped"));

        var alice = await _store.ListSwarmsAsync(new SwarmListFilter(WorkspaceId: "ws-alice"));
        Assert.Single(alice);
        Assert.Contains("s-alice", alice);
    }

    [Fact]
    public async Task ListSwarms_FilterByProject_OnlyMatchingRows()
    {
        await _store.RecordEventAsync(MakeRecord("s1", projectId: "proj-a"));
        await _store.RecordEventAsync(MakeRecord("s2", projectId: "proj-b"));

        var inProjA = await _store.ListSwarmsAsync(new SwarmListFilter(ProjectId: "proj-a"));
        Assert.Single(inProjA);
        Assert.Equal("s1", inProjA[0]);
    }

    [Fact]
    public async Task ListSwarms_FilterByWorkspaceAndProject_AndedTogether()
    {
        await _store.RecordEventAsync(MakeRecord("s1", workspaceId: "ws1", projectId: "proj-a"));
        await _store.RecordEventAsync(MakeRecord("s2", workspaceId: "ws1", projectId: "proj-b"));
        await _store.RecordEventAsync(MakeRecord("s3", workspaceId: "ws2", projectId: "proj-a"));

        var matched = await _store.ListSwarmsAsync(new SwarmListFilter(WorkspaceId: "ws1", ProjectId: "proj-a"));
        Assert.Single(matched);
        Assert.Equal("s1", matched[0]);
    }

    [Fact]
    public async Task ListSwarms_RespectsLimit()
    {
        for (var i = 0; i < 5; i++)
            await _store.RecordEventAsync(MakeRecord($"s{i}"));

        var limited = await _store.ListSwarmsAsync(new SwarmListFilter(Limit: 2));
        Assert.Equal(2, limited.Count);
    }

    [Fact]
    public async Task Exists_TrueForRecorded_FalseForMissing()
    {
        await _store.RecordEventAsync(MakeRecord("present"));
        Assert.True(await _store.ExistsAsync("present"));
        Assert.False(await _store.ExistsAsync("absent"));
    }

    [Fact]
    public async Task DeleteSwarm_RemovesAllRowsForId()
    {
        await _store.RecordEventAsync(MakeRecord("s1", eventType: "PlanCreated"));
        await _store.RecordEventAsync(MakeRecord("s1", eventType: "TaskCompleted"));
        await _store.RecordEventAsync(MakeRecord("s2"));

        var deleted = await _store.DeleteSwarmAsync("s1");
        Assert.Equal(2, deleted);

        Assert.False(await _store.ExistsAsync("s1"));
        Assert.True(await _store.ExistsAsync("s2"));
    }

    [Fact]
    public async Task RecordEvent_NullScopingFields_PersistAsNull()
    {
        await _store.RecordEventAsync(MakeRecord("s1"));

        var loaded = await _store.LoadEventsAsync("s1");
        var single = Assert.Single(loaded);
        Assert.Null(single.WorkspaceId);
        Assert.Null(single.ProjectId);
        Assert.Null(single.AgentId);
    }
}
