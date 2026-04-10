using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Missions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Missions;

public sealed class ParallelMissionExecutorTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMissionStore _store;
    private readonly SqliteMissionScratchpadStore _scratchpad;

    public ParallelMissionExecutorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_parallel_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMissionStore(_provider);
        _scratchpad = new SqliteMissionScratchpadStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    // ── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakeStepRunner : IStepRunner
    {
        public int Calls;
        public StepOutcome? Override { get; set; }

        public Task<StepOutcome> RunAsync(RuntimeStep step, StepExecutionContext context, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            var now = DateTimeOffset.UtcNow;
            var outcome = Override ?? new StepOutcome(
                StepIndex: step.Index,
                Status: StepStatus.Succeeded,
                Summary: $"step {step.Index} done",
                ObservationJson: null,
                MatchedExpectation: true,
                StartedAt: now,
                CompletedAt: now);
            return Task.FromResult(outcome);
        }
    }

    private sealed class MultiStepPlanner : IMissionPlanner
    {
        public int StepCount { get; init; } = 3;

        public Task<RuntimePlan> PlanAsync(Mission mission, CancellationToken ct = default)
        {
            var steps = new List<RuntimeStep>();
            for (int i = 0; i < StepCount; i++)
            {
                steps.Add(new RuntimeStep(i, $"step {i}", "done", RuntimeModelTier.Standard));
            }
            return Task.FromResult(new RuntimePlan(
                $"plan-{Guid.NewGuid():N}", 1, mission.Goal, steps, DateTimeOffset.UtcNow));
        }
    }

    private ParallelMissionExecutor MakeExecutor(
        IStepRunner stepRunner,
        IMissionPlanner? planner = null) =>
        new(_store, planner ?? new MultiStepPlanner(), stepRunner,
            new SqliteRuntimeTraceStore(_provider),
            new AllStepsSucceededGate(), _scratchpad,
            NullLogger<ParallelMissionExecutor>.Instance);

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_MultipleSteps_RunsAllAndCompletes()
    {
        var mission = await _store.CreateAsync("parallel test");
        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner, new MultiStepPlanner { StepCount = 3 });

        var result = await executor.RunAsync(mission.Id);

        Assert.Equal(MissionStatus.Completed, result.Status);
        Assert.Equal(3, runner.Calls);
    }

    [Fact]
    public async Task RunAsync_SingleStep_RunsSequentially()
    {
        var mission = await _store.CreateAsync("single step");
        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner, new MultiStepPlanner { StepCount = 1 });

        var result = await executor.RunAsync(mission.Id);

        Assert.Equal(MissionStatus.Completed, result.Status);
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public async Task RunAsync_ParallelSteps_WriteToScratchpad()
    {
        var mission = await _store.CreateAsync("scratchpad test");
        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner, new MultiStepPlanner { StepCount = 2 });

        await executor.RunAsync(mission.Id);

        var entries = await _scratchpad.LoadAsync(mission.Id, @namespace: "step_outcome");
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task RunAsync_StepFailure_MarksMissionFailed()
    {
        var mission = await _store.CreateAsync("will fail");
        var now = DateTimeOffset.UtcNow;
        var runner = new FakeStepRunner
        {
            Override = new StepOutcome(0, StepStatus.Failed, "boom", null, false, now, now, "boom"),
        };
        var executor = MakeExecutor(runner);

        var result = await executor.RunAsync(mission.Id);

        Assert.Equal(MissionStatus.Failed, result.Status);
    }

    [Fact]
    public async Task RunAsync_TerminalMission_IsIdempotent()
    {
        var mission = await _store.CreateAsync("already done");
        await _store.UpdateStateAsync(mission.Id, MissionStatus.Completed,
            completedAt: DateTimeOffset.UtcNow);

        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner);
        var result = await executor.RunAsync(mission.Id);

        Assert.Equal(MissionStatus.Completed, result.Status);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task RunAsync_JournalsParallelFlag()
    {
        var mission = await _store.CreateAsync("journal parallel");
        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner, new MultiStepPlanner { StepCount = 2 });

        await executor.RunAsync(mission.Id);

        var events = await _store.GetEventsAsync(mission.Id);
        var runStarted = events.First(e => e.EventType == MissionEventTypes.RunStarted);
        Assert.Contains("\"parallel\":true", runStarted.PayloadJson, StringComparison.Ordinal);
    }
}
