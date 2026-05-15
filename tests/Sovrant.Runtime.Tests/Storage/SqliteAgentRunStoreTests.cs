using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteAgentRunStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteAgentRunStore _store;

    public SqliteAgentRunStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_runs_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteAgentRunStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static AgentRunRecord MakeRun(string? kind = null, string? teamId = null) => new(
        RunId: $"run-{Guid.NewGuid():N}",
        ParentRunId: null,
        TeamId: teamId,
        MemberId: null,
        WorkspaceId: "ws-1",
        ProjectId: null,
        UserId: "alice",
        Kind: kind ?? "delegation",
        Status: "queued",
        StartedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Create_And_Get_RoundTrips()
    {
        var run = MakeRun();
        await _store.CreateAsync(run);

        var got = await _store.GetAsync(run.RunId);

        Assert.NotNull(got);
        Assert.Equal(run.RunId, got.RunId);
        Assert.Equal("delegation", got.Kind);
        Assert.Equal("queued", got.Status);
        Assert.Equal("alice", got.UserId);
    }

    [Fact]
    public async Task Get_Unknown_ReturnsNull()
    {
        Assert.Null(await _store.GetAsync("nonexistent"));
    }

    [Fact]
    public async Task UpdateStatus_SetsStatusAndEndedAt()
    {
        var run = MakeRun();
        await _store.CreateAsync(run);

        await _store.UpdateStatusAsync(run.RunId, "succeeded", inputTokens: 100, outputTokens: 50);

        var got = await _store.GetAsync(run.RunId);
        Assert.NotNull(got);
        Assert.Equal("succeeded", got.Status);
        Assert.NotNull(got.EndedAt);
        Assert.Equal(100, got.InputTokens);
        Assert.Equal(50, got.OutputTokens);
    }

    [Fact]
    public async Task List_FiltersByKind()
    {
        await _store.CreateAsync(MakeRun(kind: "delegation"));
        await _store.CreateAsync(MakeRun(kind: "swarm-task"));
        await _store.CreateAsync(MakeRun(kind: "delegation"));

        var delegations = await _store.ListAsync(new AgentRunFilter(Kind: "delegation"));
        var swarms = await _store.ListAsync(new AgentRunFilter(Kind: "swarm-task"));

        Assert.Equal(2, delegations.Count);
        Assert.Single(swarms);
    }

    [Fact]
    public async Task List_FiltersByTeam()
    {
        await _store.CreateAsync(MakeRun(teamId: "team-a"));
        await _store.CreateAsync(MakeRun(teamId: "team-b"));

        var teamA = await _store.ListAsync(new AgentRunFilter(TeamId: "team-a"));
        Assert.Single(teamA);
    }

    [Fact]
    public async Task List_FiltersByWorkspace()
    {
        await _store.CreateAsync(MakeRun());
        await _store.CreateAsync(new AgentRunRecord(
            $"run-{Guid.NewGuid():N}", null, null, null, "ws-other", null,
            "bob", "delegation", "queued", DateTimeOffset.UtcNow));

        var ws1 = await _store.ListAsync(new AgentRunFilter(WorkspaceId: "ws-1"));
        Assert.Single(ws1);
    }

    [Fact]
    public async Task List_RespectsLimit()
    {
        for (var i = 0; i < 5; i++)
            await _store.CreateAsync(MakeRun());

        var limited = await _store.ListAsync(limit: 3);
        Assert.Equal(3, limited.Count);
    }

    [Fact]
    public async Task ParentRunId_Persists()
    {
        var parent = MakeRun();
        await _store.CreateAsync(parent);

        var child = new AgentRunRecord(
            $"run-{Guid.NewGuid():N}", parent.RunId, null, null, "ws-1", null,
            "alice", "swarm-task", "running", DateTimeOffset.UtcNow);
        await _store.CreateAsync(child);

        var got = await _store.GetAsync(child.RunId);
        Assert.NotNull(got);
        Assert.Equal(parent.RunId, got.ParentRunId);

        var children = await _store.ListAsync(new AgentRunFilter(ParentRunId: parent.RunId));
        Assert.Single(children);
    }
}
