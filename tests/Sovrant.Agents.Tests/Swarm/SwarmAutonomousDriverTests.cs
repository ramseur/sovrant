using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Swarm;
using Sovrant.Runtime.Missions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Tests.Swarm;

public sealed class SwarmAutonomousDriverTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMissionStore _store;

    public SwarmAutonomousDriverTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_swarm_driver_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMissionStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private sealed class FakeDecomposer : ISwarmDecomposer
    {
        public int Calls { get; private set; }
        public SwarmPlan Plan { get; set; } = MakePlan();

        public Task<SwarmPlan> DecomposeAsync(string prompt, SwarmConfig config, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Plan);
        }
    }

    private sealed class FakeOrchestrator : ISwarmOrchestrator
    {
        public int Calls { get; private set; }
        public SwarmResult Result { get; set; } = default!;
        public Exception? Throw { get; set; }
        public List<SwarmEvent> EventsToEmit { get; } = [];

        public Task<SwarmResult> ExecuteAsync(
            SwarmPlan plan,
            SwarmConfig config,
            Action<SwarmEvent>? onEvent = null,
            SwarmExecutionContext? executionContext = null,
            CancellationToken ct = default)
        {
            Calls++;
            if (onEvent is not null)
                foreach (var ev in EventsToEmit) onEvent(ev);
            if (Throw is not null) throw Throw;
            return Task.FromResult(Result);
        }
    }

    private static SwarmPlan MakePlan() => new()
    {
        Id = "swarm-1",
        OriginalPrompt = "test",
        Tasks = new List<SwarmTaskNode>
        {
            new() { Id = "t1", Description = "do thing", Wave = 0 },
        },
        WaveCount = 1,
    };

    private static SwarmResult MakeResult(SwarmStatus status = SwarmStatus.Completed) => new()
    {
        SwarmId = "swarm-1",
        Status = status,
        Tasks = new List<SwarmTaskNode> { new() { Id = "t1", Description = "do thing" } },
        TotalTokensUsed = 42,
        Duration = TimeSpan.FromSeconds(1),
        CombinedOutput = "output",
    };

    private SwarmAutonomousDriver CreateDriver(FakeDecomposer decomposer, FakeOrchestrator orchestrator) =>
        new(_store, decomposer, orchestrator, new SwarmConfig(), NullLogger<SwarmAutonomousDriver>.Instance);

    [Fact]
    public void Name_And_Capabilities_AreStable()
    {
        var driver = CreateDriver(new FakeDecomposer(), new FakeOrchestrator { Result = MakeResult() });
        Assert.Equal("swarm", driver.Name);
        Assert.Equal(SwarmAutonomousDriver.DriverName, driver.Name);
        Assert.True(driver.Capabilities.SupportsParallelSteps);
        Assert.False(driver.Capabilities.SupportsHumanAcceptance);
    }

    [Fact]
    public async Task AdvanceAsync_HappyPath_Completes_AndJournalsTransitions()
    {
        var mission = await _store.CreateAsync("test goal");
        var decomposer = new FakeDecomposer();
        var orchestrator = new FakeOrchestrator { Result = MakeResult() };
        var driver = CreateDriver(decomposer, orchestrator);

        var updated = await driver.AdvanceAsync(mission.Id);

        Assert.Equal(MissionStatus.Completed, updated.Status);
        Assert.NotNull(updated.CompletedAt);
        Assert.Equal(1, decomposer.Calls);
        Assert.Equal(1, orchestrator.Calls);

        var events = await _store.GetEventsAsync(mission.Id);
        var types = events.Select(e => e.EventType).ToList();
        Assert.Contains(MissionEventTypes.PlanRevised, types);
        Assert.Contains(MissionEventTypes.RunStarted, types);
        Assert.Contains(MissionEventTypes.RunCompleted, types);
        Assert.Contains(MissionEventTypes.Completed, types);
    }

    [Fact]
    public async Task AdvanceAsync_ProjectsSwarmEvents_ToMissionJournal()
    {
        var mission = await _store.CreateAsync("projected events");
        var orchestrator = new FakeOrchestrator { Result = MakeResult() };
        orchestrator.EventsToEmit.Add(new SwarmEvent.TaskStarted("swarm-1", "t1", "coder", "do thing", 0));
        orchestrator.EventsToEmit.Add(new SwarmEvent.TaskCompleted("swarm-1", "t1", "coder", "do thing", "done", 10));
        orchestrator.EventsToEmit.Add(new SwarmEvent.WaveCompleted("swarm-1", 0, 1, 0));

        var driver = CreateDriver(new FakeDecomposer(), orchestrator);
        await driver.AdvanceAsync(mission.Id);

        var events = await _store.GetEventsAsync(mission.Id);
        var types = events.Select(e => e.EventType).ToList();
        Assert.Contains("swarm_task_started", types);
        Assert.Contains("swarm_task_completed", types);
        Assert.Contains("swarm_wave_completed", types);
    }

    [Fact]
    public async Task AdvanceAsync_OrchestratorThrows_MarksFailed()
    {
        var mission = await _store.CreateAsync("will fail");
        var orchestrator = new FakeOrchestrator { Throw = new InvalidOperationException("swarm down") };
        var driver = CreateDriver(new FakeDecomposer(), orchestrator);

        var updated = await driver.AdvanceAsync(mission.Id);

        Assert.Equal(MissionStatus.Failed, updated.Status);
        var events = await _store.GetEventsAsync(mission.Id);
        Assert.Contains(MissionEventTypes.Failed, events.Select(e => e.EventType));
    }

    [Fact]
    public async Task AdvanceAsync_SwarmReturnsFailed_MarksFailed()
    {
        var mission = await _store.CreateAsync("returns failed");
        var orchestrator = new FakeOrchestrator { Result = MakeResult(SwarmStatus.Failed) };
        var driver = CreateDriver(new FakeDecomposer(), orchestrator);

        var updated = await driver.AdvanceAsync(mission.Id);

        Assert.Equal(MissionStatus.Failed, updated.Status);
    }

    [Fact]
    public async Task AdvanceAsync_TerminalMission_IsNoOp()
    {
        var mission = await _store.CreateAsync("already done");
        await _store.UpdateStateAsync(mission.Id, MissionStatus.Completed, completedAt: DateTimeOffset.UtcNow);

        var decomposer = new FakeDecomposer();
        var orchestrator = new FakeOrchestrator { Result = MakeResult() };
        var driver = CreateDriver(decomposer, orchestrator);

        var updated = await driver.AdvanceAsync(mission.Id);

        Assert.Equal(MissionStatus.Completed, updated.Status);
        Assert.Equal(0, decomposer.Calls);
        Assert.Equal(0, orchestrator.Calls);
    }

    [Fact]
    public async Task AdvanceAsync_UnknownMissionId_Throws()
    {
        var driver = CreateDriver(new FakeDecomposer(), new FakeOrchestrator { Result = MakeResult() });
        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.AdvanceAsync("does-not-exist"));
    }
}
