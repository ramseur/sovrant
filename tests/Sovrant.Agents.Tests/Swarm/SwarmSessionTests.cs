using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Swarm;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Tests.Swarm;

/// <summary>
/// Round-trip tests for <see cref="SwarmSession"/> against a real
/// <see cref="SqliteSwarmEventStore"/> backed by a temp SQLite file.
/// As of Phase 37.5 the session writes to the <c>swarm_events</c> table
/// rather than JSONL files, so these tests now also exercise the SQLite
/// store and the workspace-scoping path.
/// </summary>
public class SwarmSessionTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SwarmSession _session;

    public SwarmSessionTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"swarm_session_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        var store = new SqliteSwarmEventStore(_provider);
        _session = new SwarmSession(store);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task RecordAndReplay_RoundTrips()
    {
        var swarmId = "test-swarm-1";
        var evt = new SwarmEvent.PlanCreated(swarmId, 3, 2);

        await _session.RecordAsync(evt);

        var events = new List<SwarmEvent?>();
        await foreach (var e in _session.ReplayAsync(swarmId))
            events.Add(e);

        Assert.Single(events);
        var replayed = Assert.IsType<SwarmEvent.PlanCreated>(events[0]);
        Assert.Equal(swarmId, replayed.SwarmId);
        Assert.Equal(3, replayed.TaskCount);
        Assert.Equal(2, replayed.WaveCount);
    }

    [Fact]
    public async Task RecordMultipleEvents_ReplaysInOrder()
    {
        var swarmId = "test-swarm-2";
        await _session.RecordAsync(new SwarmEvent.PlanCreated(swarmId, 2, 1));
        await _session.RecordAsync(new SwarmEvent.TaskStarted(swarmId, "t1", "coder", "Write the code", 0));
        await _session.RecordAsync(new SwarmEvent.TaskCompleted(swarmId, "t1", "coder", "Write the code", "done", 500));

        var events = new List<SwarmEvent?>();
        await foreach (var e in _session.ReplayAsync(swarmId))
            events.Add(e);

        Assert.Equal(3, events.Count);
        Assert.IsType<SwarmEvent.PlanCreated>(events[0]);
        Assert.IsType<SwarmEvent.TaskStarted>(events[1]);
        Assert.IsType<SwarmEvent.TaskCompleted>(events[2]);
    }

    [Fact]
    public async Task Replay_NonexistentSession_ReturnsEmpty()
    {
        var events = new List<SwarmEvent?>();
        await foreach (var e in _session.ReplayAsync("nonexistent"))
            events.Add(e);

        Assert.Empty(events);
    }

    [Fact]
    public async Task ListSessions_ReturnsRecordedSessions()
    {
        await _session.RecordAsync(new SwarmEvent.PlanCreated("s1", 1, 1));
        await _session.RecordAsync(new SwarmEvent.PlanCreated("s2", 1, 1));

        var sessions = await _session.ListSessionsAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Contains("s1", sessions);
        Assert.Contains("s2", sessions);
    }

    [Fact]
    public async Task ListSessions_EmptyStore_ReturnsEmpty()
    {
        Assert.Empty(await _session.ListSessionsAsync());
    }

    [Fact]
    public async Task Exists_ReturnsTrueForRecordedSession()
    {
        await _session.RecordAsync(new SwarmEvent.PlanCreated("s1", 1, 1));
        Assert.True(await _session.ExistsAsync("s1"));
    }

    [Fact]
    public async Task Exists_ReturnsFalseForNonexistentSession()
    {
        Assert.False(await _session.ExistsAsync("nonexistent"));
    }

    [Fact]
    public async Task RecordTaskFailed_Replays()
    {
        var swarmId = "test-swarm-3";
        await _session.RecordAsync(new SwarmEvent.TaskFailed(swarmId, "t1", "coder", "Write the code", "timeout", 2));

        var events = new List<SwarmEvent?>();
        await foreach (var e in _session.ReplayAsync(swarmId))
            events.Add(e);

        var failed = Assert.IsType<SwarmEvent.TaskFailed>(events[0]);
        Assert.Equal("timeout", failed.Error);
        Assert.Equal(2, failed.RetryCount);
    }

    [Fact]
    public async Task RecordFileConflict_Replays()
    {
        var swarmId = "test-swarm-4";
        await _session.RecordAsync(new SwarmEvent.FileConflict(swarmId, "t2", "reviewer", "/src/foo.cs", "t1", "coder"));

        var events = new List<SwarmEvent?>();
        await foreach (var e in _session.ReplayAsync(swarmId))
            events.Add(e);

        var conflict = Assert.IsType<SwarmEvent.FileConflict>(events[0]);
        Assert.Equal("/src/foo.cs", conflict.FilePath);
        Assert.Equal("t1", conflict.HeldByTaskId);
    }

    [Fact]
    public async Task RecordBudgetExceeded_Replays()
    {
        var swarmId = "test-swarm-5";
        await _session.RecordAsync(new SwarmEvent.BudgetExceeded(swarmId, 600000, 500000));

        var events = new List<SwarmEvent?>();
        await foreach (var e in _session.ReplayAsync(swarmId))
            events.Add(e);

        var budget = Assert.IsType<SwarmEvent.BudgetExceeded>(events[0]);
        Assert.Equal(600000, budget.Used);
        Assert.Equal(500000, budget.Limit);
    }

    [Fact]
    public async Task RecordSwarmCompleted_Replays()
    {
        var swarmId = "test-swarm-6";
        await _session.RecordAsync(new SwarmEvent.SwarmCompleted(swarmId, SwarmStatus.Completed, 10000, 5.5));

        var events = new List<SwarmEvent?>();
        await foreach (var e in _session.ReplayAsync(swarmId))
            events.Add(e);

        var completed = Assert.IsType<SwarmEvent.SwarmCompleted>(events[0]);
        Assert.Equal(SwarmStatus.Completed, completed.FinalStatus);
        Assert.Equal(10000, completed.TotalTokens);
    }

    [Fact]
    public async Task RecordWithContext_StampsWorkspaceAndProject()
    {
        var swarmId = "scoped-swarm";
        var context = new SwarmExecutionContext(
            UserId: "alice",
            WorkspaceId: "ws-personal-alice",
            ProjectId: "proj-1");

        await _session.RecordAsync(new SwarmEvent.PlanCreated(swarmId, 1, 1), context);

        // The session can find it back
        Assert.True(await _session.ExistsAsync(swarmId));

        // And ListSessions filtered by workspace surfaces it; filtering by a different workspace does not.
        var inWorkspace = await _session.ListSessionsAsync(new SwarmListFilter(WorkspaceId: "ws-personal-alice"));
        Assert.Contains(swarmId, inWorkspace);

        var inOtherWorkspace = await _session.ListSessionsAsync(new SwarmListFilter(WorkspaceId: "ws-other"));
        Assert.DoesNotContain(swarmId, inOtherWorkspace);

        // Project filter is independent.
        var inProject = await _session.ListSessionsAsync(new SwarmListFilter(ProjectId: "proj-1"));
        Assert.Contains(swarmId, inProject);
    }

    [Fact]
    public async Task ListSessions_WithNoFilter_ReturnsAllScopes()
    {
        await _session.RecordAsync(
            new SwarmEvent.PlanCreated("s-personal", 1, 1),
            new SwarmExecutionContext(WorkspaceId: "ws-personal-alice"));
        await _session.RecordAsync(
            new SwarmEvent.PlanCreated("s-team", 1, 1),
            new SwarmExecutionContext(WorkspaceId: "ws-team-1"));
        await _session.RecordAsync(new SwarmEvent.PlanCreated("s-unscoped", 1, 1));

        var all = await _session.ListSessionsAsync();
        Assert.Equal(3, all.Count);
        Assert.Contains("s-personal", all);
        Assert.Contains("s-team", all);
        Assert.Contains("s-unscoped", all);
    }
}
