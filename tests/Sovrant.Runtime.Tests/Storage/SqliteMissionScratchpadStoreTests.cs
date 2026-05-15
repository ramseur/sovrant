using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteMissionScratchpadStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IMissionScratchpadStore _store;

    public SqliteMissionScratchpadStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_mission_scratchpad_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMissionScratchpadStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private static MissionScratchpadEntry MakeEntry(
        string missionId = "mission-1",
        int stepIndex = 0,
        string @namespace = "findings",
        string key = "suspect-file",
        string value = """{"path":"src/Foo.cs"}""",
        string? agentId = null,
        string? workspaceId = null,
        string? projectId = null) =>
        new(
            MissionId: missionId,
            StepIndex: stepIndex,
            Namespace: @namespace,
            Key: key,
            Value: value,
            AgentId: agentId,
            WorkspaceId: workspaceId,
            ProjectId: projectId);

    [Fact]
    public async Task AppendAndLoad_RoundTripsAllFields()
    {
        var entry = MakeEntry(
            missionId: "m-round",
            stepIndex: 3,
            @namespace: "hypotheses",
            key: "root-cause",
            value: """{"confidence":0.8}""",
            agentId: "agent-7",
            workspaceId: "ws-alice",
            projectId: "proj-portfolio");

        await _store.AppendAsync(entry);
        var rows = await _store.LoadAsync("m-round");

        var row = Assert.Single(rows);
        Assert.Equal("m-round", row.MissionId);
        Assert.Equal(3, row.StepIndex);
        Assert.Equal("hypotheses", row.Namespace);
        Assert.Equal("root-cause", row.Key);
        Assert.Equal("""{"confidence":0.8}""", row.Value);
        Assert.Equal("agent-7", row.AgentId);
        Assert.Equal("ws-alice", row.WorkspaceId);
        Assert.Equal("proj-portfolio", row.ProjectId);
        // SQLite stamps created_at; should be close to now.
        Assert.True(row.CreatedAt != default);
    }

    [Fact]
    public async Task Load_ReturnsEntriesInInsertionOrder()
    {
        await _store.AppendAsync(MakeEntry(missionId: "m-order", key: "k1", value: "1"));
        await _store.AppendAsync(MakeEntry(missionId: "m-order", key: "k2", value: "2"));
        await _store.AppendAsync(MakeEntry(missionId: "m-order", key: "k3", value: "3"));

        var rows = await _store.LoadAsync("m-order");

        Assert.Equal(new[] { "k1", "k2", "k3" }, rows.Select(r => r.Key).ToArray());
        Assert.Equal(new[] { "1", "2", "3" }, rows.Select(r => r.Value).ToArray());
    }

    [Fact]
    public async Task Load_FiltersByStepIndex()
    {
        await _store.AppendAsync(MakeEntry(missionId: "m-step", stepIndex: 0, key: "k0"));
        await _store.AppendAsync(MakeEntry(missionId: "m-step", stepIndex: 1, key: "k1"));
        await _store.AppendAsync(MakeEntry(missionId: "m-step", stepIndex: 1, key: "k1b"));
        await _store.AppendAsync(MakeEntry(missionId: "m-step", stepIndex: 2, key: "k2"));

        var rows = await _store.LoadAsync("m-step", stepIndex: 1);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(1, r.StepIndex));
        Assert.Equal(new[] { "k1", "k1b" }, rows.Select(r => r.Key).ToArray());
    }

    [Fact]
    public async Task Load_FiltersByNamespace()
    {
        await _store.AppendAsync(MakeEntry(missionId: "m-ns", @namespace: "findings", key: "a"));
        await _store.AppendAsync(MakeEntry(missionId: "m-ns", @namespace: "blockers", key: "b"));
        await _store.AppendAsync(MakeEntry(missionId: "m-ns", @namespace: "findings", key: "c"));

        var rows = await _store.LoadAsync("m-ns", @namespace: "findings");

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("findings", r.Namespace));
    }

    [Fact]
    public async Task Load_IsolatesByMissionId()
    {
        await _store.AppendAsync(MakeEntry(missionId: "m-a", key: "ak"));
        await _store.AppendAsync(MakeEntry(missionId: "m-b", key: "bk"));

        var aRows = await _store.LoadAsync("m-a");
        var bRows = await _store.LoadAsync("m-b");

        Assert.Equal("ak", Assert.Single(aRows).Key);
        Assert.Equal("bk", Assert.Single(bRows).Key);
    }

    [Fact]
    public async Task ReadLatest_ReturnsNullWhenMissing()
    {
        var row = await _store.ReadLatestAsync("m-empty", "findings", "nope");
        Assert.Null(row);
    }

    [Fact]
    public async Task ReadLatest_ReturnsMostRecentValueForKey()
    {
        await _store.AppendAsync(MakeEntry(missionId: "m-latest", key: "state", value: "v1"));
        await _store.AppendAsync(MakeEntry(missionId: "m-latest", key: "state", value: "v2"));
        await _store.AppendAsync(MakeEntry(missionId: "m-latest", key: "state", value: "v3"));
        // Different key should not interfere.
        await _store.AppendAsync(MakeEntry(missionId: "m-latest", key: "other", value: "x"));

        var latest = await _store.ReadLatestAsync("m-latest", "findings", "state");

        Assert.NotNull(latest);
        Assert.Equal("v3", latest!.Value);
    }

    [Fact]
    public async Task ReadLatest_ScopedByNamespace()
    {
        await _store.AppendAsync(MakeEntry(missionId: "m-nsl", @namespace: "findings", key: "k", value: "f"));
        await _store.AppendAsync(MakeEntry(missionId: "m-nsl", @namespace: "blockers", key: "k", value: "b"));

        var findings = await _store.ReadLatestAsync("m-nsl", "findings", "k");
        var blockers = await _store.ReadLatestAsync("m-nsl", "blockers", "k");

        Assert.Equal("f", findings!.Value);
        Assert.Equal("b", blockers!.Value);
    }

    [Fact]
    public async Task Append_IsAppendOnly_NoOverwrite()
    {
        await _store.AppendAsync(MakeEntry(missionId: "m-append", key: "k", value: "v1"));
        await _store.AppendAsync(MakeEntry(missionId: "m-append", key: "k", value: "v2"));

        var rows = await _store.LoadAsync("m-append");
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "v1", "v2" }, rows.Select(r => r.Value).ToArray());
    }

    [Fact]
    public async Task DeleteMission_RemovesAllEntriesForThatMissionOnly()
    {
        await _store.AppendAsync(MakeEntry(missionId: "m-del", key: "k1"));
        await _store.AppendAsync(MakeEntry(missionId: "m-del", key: "k2"));
        await _store.AppendAsync(MakeEntry(missionId: "m-keep", key: "k"));

        var deleted = await _store.DeleteMissionAsync("m-del");

        Assert.Equal(2, deleted);
        Assert.Empty(await _store.LoadAsync("m-del"));
        Assert.Single(await _store.LoadAsync("m-keep"));
    }

    [Fact]
    public async Task Append_DefaultsAgentIdToNullWhenNotSupplied()
    {
        await _store.AppendAsync(MakeEntry(missionId: "m-agent", agentId: null));
        var row = Assert.Single(await _store.LoadAsync("m-agent"));
        Assert.Null(row.AgentId);
    }
}
