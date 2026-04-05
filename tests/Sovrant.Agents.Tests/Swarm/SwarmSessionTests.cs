using Sovrant.Agents.Swarm;

namespace Sovrant.Agents.Tests.Swarm;

public class SwarmSessionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SwarmSession _session;

    public SwarmSessionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"swarm-test-{Guid.NewGuid():N}");
        _session = new SwarmSession(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
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
        await _session.RecordAsync(new SwarmEvent.TaskStarted(swarmId, "t1", "coder", 0));
        await _session.RecordAsync(new SwarmEvent.TaskCompleted(swarmId, "t1", "done", 500));

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

        var sessions = _session.ListSessions();
        Assert.Equal(2, sessions.Count);
        Assert.Contains("s1", sessions);
        Assert.Contains("s2", sessions);
    }

    [Fact]
    public void ListSessions_EmptyDir_ReturnsEmpty()
    {
        Assert.Empty(_session.ListSessions());
    }

    [Fact]
    public async Task Exists_ReturnsTrueForRecordedSession()
    {
        await _session.RecordAsync(new SwarmEvent.PlanCreated("s1", 1, 1));
        Assert.True(_session.Exists("s1"));
    }

    [Fact]
    public void Exists_ReturnsFalseForNonexistentSession()
    {
        Assert.False(_session.Exists("nonexistent"));
    }

    [Fact]
    public async Task RecordTaskFailed_Replays()
    {
        var swarmId = "test-swarm-3";
        await _session.RecordAsync(new SwarmEvent.TaskFailed(swarmId, "t1", "timeout", 2));

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
        await _session.RecordAsync(new SwarmEvent.FileConflict(swarmId, "t2", "/src/foo.cs", "t1"));

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
}
