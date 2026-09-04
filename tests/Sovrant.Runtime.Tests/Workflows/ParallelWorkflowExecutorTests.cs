using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Workflows;

public sealed class ParallelWorkflowExecutorTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteWorkflowStore _store;
    private readonly SqliteWorkflowScratchpadStore _scratchpad;

    public ParallelWorkflowExecutorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_parallel_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkflowStore(_provider);
        _scratchpad = new SqliteWorkflowScratchpadStore(_provider);
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

    private sealed class MultiStepPlanner : IWorkflowPlanner
    {
        public int StepCount { get; init; } = 3;

        public Task<RuntimePlan> PlanAsync(Workflow workflow, CancellationToken ct = default)
        {
            var steps = new List<RuntimeStep>();
            for (int i = 0; i < StepCount; i++)
            {
                steps.Add(new RuntimeStep(i, $"step {i}", "done", RuntimeModelTier.Standard));
            }
            return Task.FromResult(new RuntimePlan(
                $"plan-{Guid.NewGuid():N}", 1, workflow.Goal, steps, DateTimeOffset.UtcNow));
        }
    }

    private ParallelWorkflowExecutor MakeExecutor(
        IStepRunner stepRunner,
        IWorkflowPlanner? planner = null) =>
        new(_store, planner ?? new MultiStepPlanner(), stepRunner,
            new SqliteRuntimeTraceStore(_provider),
            new AllStepsSucceededGate(), _scratchpad,
            NullLogger<ParallelWorkflowExecutor>.Instance);

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_MultipleSteps_RunsAllAndCompletes()
    {
        var workflow = await _store.CreateAsync("parallel test");
        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner, new MultiStepPlanner { StepCount = 3 });

        var result = await executor.RunAsync(workflow.Id);

        Assert.Equal(WorkflowStatus.Completed, result.Status);
        Assert.Equal(3, runner.Calls);
    }

    [Fact]
    public async Task RunAsync_SingleStep_RunsSequentially()
    {
        var workflow = await _store.CreateAsync("single step");
        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner, new MultiStepPlanner { StepCount = 1 });

        var result = await executor.RunAsync(workflow.Id);

        Assert.Equal(WorkflowStatus.Completed, result.Status);
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public async Task RunAsync_ParallelSteps_WriteToScratchpad()
    {
        var workflow = await _store.CreateAsync("scratchpad test");
        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner, new MultiStepPlanner { StepCount = 2 });

        await executor.RunAsync(workflow.Id);

        var entries = await _scratchpad.LoadAsync(workflow.Id, @namespace: "step_outcome");
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task RunAsync_StepFailure_MarksWorkflowFailed()
    {
        var workflow = await _store.CreateAsync("will fail");
        var now = DateTimeOffset.UtcNow;
        var runner = new FakeStepRunner
        {
            Override = new StepOutcome(0, StepStatus.Failed, "boom", null, false, now, now, "boom"),
        };
        var executor = MakeExecutor(runner);

        var result = await executor.RunAsync(workflow.Id);

        Assert.Equal(WorkflowStatus.Failed, result.Status);
    }

    [Fact]
    public async Task RunAsync_TerminalWorkflow_IsIdempotent()
    {
        var workflow = await _store.CreateAsync("already done");
        await _store.UpdateStateAsync(workflow.Id, WorkflowStatus.Completed,
            completedAt: DateTimeOffset.UtcNow);

        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner);
        var result = await executor.RunAsync(workflow.Id);

        Assert.Equal(WorkflowStatus.Completed, result.Status);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task RunAsync_JournalsParallelFlag()
    {
        var workflow = await _store.CreateAsync("journal parallel");
        var runner = new FakeStepRunner();
        var executor = MakeExecutor(runner, new MultiStepPlanner { StepCount = 2 });

        await executor.RunAsync(workflow.Id);

        var events = await _store.GetEventsAsync(workflow.Id);
        var runStarted = events.First(e => e.EventType == WorkflowEventTypes.RunStarted);
        Assert.Contains("\"parallel\":true", runStarted.PayloadJson, StringComparison.Ordinal);
    }
}
