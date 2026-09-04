using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Workflows;

public sealed class LlmWorkflowExecutorTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteWorkflowStore _store;

    public LlmWorkflowExecutorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_mission_exec_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkflowStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    // ── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakeEngineExecutor : IExecutor
    {
        public ExecutionResult NextResult { get; set; } = default!;
        public Exception? Throw { get; set; }
        public int Calls { get; private set; }

        public Task<ExecutionResult> ExecuteAsync(
            RuntimePlan plan, EngineRunContext runContext, Replanner replanner, CancellationToken ct = default)
        {
            Calls++;
            if (Throw is not null) throw Throw;
            return Task.FromResult(NextResult);
        }
    }

    private static ExecutionResult OneSuccessfulStep()
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionResult(
            FinalPlan: new RuntimePlan("plan-x", 1, "g", Array.Empty<RuntimeStep>(), now),
            Outcomes: new[]
            {
                new StepOutcome(0, StepStatus.Succeeded, "did it", null, true, now, now),
            },
            ReplanCount: 0,
            TerminalState: ExecutionTerminalState.Completed);
    }

    private static ExecutionResult OneFailedStep() =>
        new(FinalPlan: new RuntimePlan("plan-x", 1, "g", Array.Empty<RuntimeStep>(), DateTimeOffset.UtcNow),
            Outcomes: new[]
            {
                new StepOutcome(0, StepStatus.Failed, "boom", null, false,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ErrorMessage: "boom"),
            },
            ReplanCount: 0,
            TerminalState: ExecutionTerminalState.FailedAfterReplans);

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_HappyPath_CompletesWorkflowAndJournalsEveryTransition()
    {
        var workflow = await _store.CreateAsync("fix the bug");
        var engine = new FakeEngineExecutor { NextResult = OneSuccessfulStep() };

        var executor = new LlmWorkflowExecutor(
            _store, new SimpleWorkflowPlanner(), engine, new AllStepsSucceededGate(),
            NullLogger<LlmWorkflowExecutor>.Instance);

        var updated = await executor.RunAsync(workflow.Id);

        Assert.Equal(WorkflowStatus.Completed, updated.Status);
        Assert.NotNull(updated.CompletedAt);
        Assert.Equal(1, engine.Calls);

        var events = await _store.GetEventsAsync(workflow.Id);
        var types = events.Select(e => e.EventType).ToList();
        Assert.Contains(WorkflowEventTypes.WorkflowCreated, types);
        Assert.Contains(WorkflowEventTypes.PlanRevised, types);
        Assert.Contains(WorkflowEventTypes.RunStarted, types);
        Assert.Contains(WorkflowEventTypes.RunCompleted, types);
        Assert.Contains(WorkflowEventTypes.AcceptanceApproved, types);
        Assert.Contains(WorkflowEventTypes.Completed, types);
    }

    [Fact]
    public async Task RunAsync_StepFailure_MarksWorkflowFailedAndJournalsRejection()
    {
        var workflow = await _store.CreateAsync("impossible goal");
        var engine = new FakeEngineExecutor { NextResult = OneFailedStep() };

        var executor = new LlmWorkflowExecutor(
            _store, new SimpleWorkflowPlanner(), engine, new AllStepsSucceededGate(),
            NullLogger<LlmWorkflowExecutor>.Instance);

        var updated = await executor.RunAsync(workflow.Id);

        Assert.Equal(WorkflowStatus.Failed, updated.Status);
        var events = await _store.GetEventsAsync(workflow.Id);
        var types = events.Select(e => e.EventType).ToList();
        Assert.Contains(WorkflowEventTypes.AcceptanceRejected, types);
        Assert.Contains(WorkflowEventTypes.Failed, types);
    }

    [Fact]
    public async Task RunAsync_EngineThrows_MarksFailedWithErrorEvent()
    {
        var workflow = await _store.CreateAsync("will crash");
        var engine = new FakeEngineExecutor { Throw = new InvalidOperationException("provider down") };

        var executor = new LlmWorkflowExecutor(
            _store, new SimpleWorkflowPlanner(), engine, new AllStepsSucceededGate(),
            NullLogger<LlmWorkflowExecutor>.Instance);

        var updated = await executor.RunAsync(workflow.Id);

        Assert.Equal(WorkflowStatus.Failed, updated.Status);
        var events = await _store.GetEventsAsync(workflow.Id);
        Assert.Contains(events, e =>
            e.EventType == WorkflowEventTypes.Failed
            && e.PayloadJson.Contains("provider down", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_TerminalWorkflow_IsIdempotent()
    {
        var workflow = await _store.CreateAsync("already done");
        await _store.UpdateStateAsync(
            workflow.Id, WorkflowStatus.Completed, completedAt: DateTimeOffset.UtcNow);

        var engine = new FakeEngineExecutor();  // would throw if called with no NextResult
        var executor = new LlmWorkflowExecutor(
            _store, new SimpleWorkflowPlanner(), engine, new AllStepsSucceededGate(),
            NullLogger<LlmWorkflowExecutor>.Instance);

        var updated = await executor.RunAsync(workflow.Id);

        Assert.Equal(WorkflowStatus.Completed, updated.Status);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public async Task RunAsync_UnknownWorkflow_Throws()
    {
        var engine = new FakeEngineExecutor();
        var executor = new LlmWorkflowExecutor(
            _store, new SimpleWorkflowPlanner(), engine, new AllStepsSucceededGate(),
            NullLogger<LlmWorkflowExecutor>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.RunAsync("workflow-does-not-exist"));
    }
}
