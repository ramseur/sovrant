using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Engine;

/// <summary>
/// Tests the re-plan loop, retry policy, and durable trace writes of
/// <see cref="LlmExecutor"/>. The step runner is a configurable fake so
/// every test controls exactly what outcomes the executor sees.
/// </summary>
public sealed class LlmExecutorTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IRuntimeTraceStore _traceStore;

    public LlmExecutorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_llm_exec_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _traceStore = new SqliteRuntimeTraceStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static RuntimeStep MakeStep(int index, string intent = "do a thing") =>
        new(
            Index: index,
            Intent: intent,
            ExpectedOutcome: "it works",
            ModelTier: RuntimeModelTier.Standard);

    private static RuntimePlan MakePlan(string id, int version, params RuntimeStep[] steps) =>
        new(
            Id: id,
            PlanVersion: version,
            Goal: "test goal",
            Steps: steps,
            CreatedAt: DateTimeOffset.UtcNow);

    private static EngineRunContext MakeRunContext(string? runId = null) =>
        new(
            RuntimeRunId: runId ?? $"run-{Guid.NewGuid():N}",
            SessionId: "sess-test",
            WorkspaceId: "ws-test",
            ProjectId: "proj-test");

    private LlmExecutor MakeExecutor(IStepRunner stepRunner, ExecutorOptions? options = null) =>
        new(stepRunner, _traceStore, LiveSettings.Static(options ?? new ExecutorOptions()), NullLogger<LlmExecutor>.Instance);

    private static StepOutcome Succeed(int stepIndex, string summary = "ok") =>
        new(stepIndex, StepStatus.Succeeded, summary, null, MatchedExpectation: true,
            StartedAt: DateTimeOffset.UtcNow, CompletedAt: DateTimeOffset.UtcNow);

    private static StepOutcome Contradict(int stepIndex, string summary = "expected X, saw Y") =>
        new(stepIndex, StepStatus.Contradicted, summary, null, MatchedExpectation: false,
            StartedAt: DateTimeOffset.UtcNow, CompletedAt: DateTimeOffset.UtcNow);

    private static StepOutcome Fail(int stepIndex, string error = "boom") =>
        new(stepIndex, StepStatus.Failed, error, null, MatchedExpectation: false,
            StartedAt: DateTimeOffset.UtcNow, CompletedAt: DateTimeOffset.UtcNow,
            ErrorMessage: error);

    // A simple fake step runner: you give it a sequence of outcomes per
    // (step index, attempt) and it returns them in call order.
    private sealed class ScriptedStepRunner : IStepRunner
    {
        private readonly Queue<StepOutcome> _script;
        public List<(int StepIndex, int Attempt)> Calls { get; } = new();

        public ScriptedStepRunner(params StepOutcome[] script) =>
            _script = new Queue<StepOutcome>(script);

        public Task<StepOutcome> RunAsync(RuntimeStep step, StepExecutionContext context, CancellationToken ct)
        {
            Calls.Add((step.Index, context.AttemptNumber));
            if (_script.Count == 0)
                throw new InvalidOperationException(
                    $"ScriptedStepRunner exhausted at step {step.Index}, attempt {context.AttemptNumber}");
            return Task.FromResult(_script.Dequeue());
        }
    }

    private sealed class ThrowingStepRunner(Exception ex) : IStepRunner
    {
        public int CallCount { get; private set; }
        public Task<StepOutcome> RunAsync(RuntimeStep step, StepExecutionContext context, CancellationToken ct)
        {
            CallCount++;
            throw ex;
        }
    }

    // ── Happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AllStepsSucceed_ReturnsCompleted()
    {
        var plan = MakePlan("p1", 1, MakeStep(0), MakeStep(1), MakeStep(2));
        var runner = new ScriptedStepRunner(Succeed(0), Succeed(1), Succeed(2));
        var exec = MakeExecutor(runner);
        var runCtx = MakeRunContext();

        var result = await exec.ExecuteAsync(
            plan, runCtx, (p, o, t, ct) => throw new Xunit.Sdk.XunitException("replanner should not be called"));

        Assert.Equal(ExecutionTerminalState.Completed, result.TerminalState);
        Assert.Equal(0, result.ReplanCount);
        Assert.Equal(3, result.Outcomes.Count);
        Assert.All(result.Outcomes, o => Assert.Equal(StepStatus.Succeeded, o.Status));
        Assert.Equal(new[] { (0, 1), (1, 1), (2, 1) }, runner.Calls);
    }

    // ── Trace ordering ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WritesTraceEntriesInOrder()
    {
        var plan = MakePlan("p2", 1, MakeStep(0), MakeStep(1));
        var runner = new ScriptedStepRunner(Succeed(0), Succeed(1));
        var exec = MakeExecutor(runner);
        var runCtx = MakeRunContext("run-trace");

        await exec.ExecuteAsync(plan, runCtx,
            (p, o, t, ct) => throw new Xunit.Sdk.XunitException("no replan expected"));

        var entries = await _traceStore.LoadAsync("run-trace");
        var types = entries.Select(e => e.EntryType).ToArray();

        Assert.Equal(new[]
        {
            "plan_created",
            "step_started", "step_completed",
            "step_started", "step_completed",
            "run_completed",
        }, types);

        // Session/workspace/project propagated to every row.
        Assert.All(entries, e =>
        {
            Assert.Equal("sess-test", e.SessionId);
            Assert.Equal("ws-test", e.WorkspaceId);
            Assert.Equal("proj-test", e.ProjectId);
        });
    }

    // ── Retry on failure ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FailedStep_RetriesWithinBudget()
    {
        var plan = MakePlan("p3", 1, MakeStep(0), MakeStep(1));
        // Step 0 fails once, then succeeds. Step 1 succeeds.
        var runner = new ScriptedStepRunner(Fail(0, "first attempt"), Succeed(0), Succeed(1));
        var exec = MakeExecutor(runner, new ExecutorOptions(MaxReplans: 3, MaxStepRetries: 2));
        var runCtx = MakeRunContext();

        var result = await exec.ExecuteAsync(
            plan, runCtx, (p, o, t, ct) => throw new Xunit.Sdk.XunitException("no replan expected"));

        Assert.Equal(ExecutionTerminalState.Completed, result.TerminalState);
        Assert.Equal(0, result.ReplanCount);
        // Outcomes: Fail(0, attempt1), Succeed(0, attempt2), Succeed(1, attempt1)
        Assert.Equal(3, result.Outcomes.Count);
        Assert.Equal(StepStatus.Failed, result.Outcomes[0].Status);
        Assert.Equal(StepStatus.Succeeded, result.Outcomes[1].Status);
        Assert.Equal(new[] { (0, 1), (0, 2), (1, 1) }, runner.Calls);
    }

    // ── Re-plan on contradiction ─────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Contradiction_TriggersReplan()
    {
        var planV1 = MakePlan("p4", 1, MakeStep(0), MakeStep(1));
        // Step 0 returns Contradicted → executor asks for a re-plan.
        var runner = new ScriptedStepRunner(
            Contradict(0, "file did not contain expected marker"),
            // After the re-plan swap, the new plan also has two steps that both succeed.
            Succeed(0),
            Succeed(1));
        var exec = MakeExecutor(runner);
        var runCtx = MakeRunContext("run-contra");

        var planV2 = MakePlan("p4", 2, MakeStep(0, "revised"), MakeStep(1, "revised"));
        ReplanTrigger? observedTrigger = null;
        Replanner replanner = (plan, outcomes, trigger, ct) =>
        {
            observedTrigger = trigger;
            return Task.FromResult(planV2);
        };

        var result = await exec.ExecuteAsync(planV1, runCtx, replanner);

        Assert.Equal(ExecutionTerminalState.Completed, result.TerminalState);
        Assert.Equal(1, result.ReplanCount);
        Assert.NotNull(observedTrigger);
        Assert.Equal(ReplanReason.Contradiction, observedTrigger!.Kind);
        Assert.Equal(0, observedTrigger.OffendingStepIndex);
        Assert.Same(planV2, result.FinalPlan);

        // Trace must contain a replan_requested entry followed by a second plan_created.
        var entries = await _traceStore.LoadAsync("run-contra");
        var types = entries.Select(e => e.EntryType).ToArray();
        Assert.Contains("replan_requested", types);
        Assert.Equal(2, types.Count(t => t == "plan_created"));
    }

    // ── Re-plan on repeated failure ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RepeatedFailure_ExhaustsRetriesThenReplans()
    {
        var planV1 = MakePlan("p5", 1, MakeStep(0));
        // Step 0 fails on all three attempts (initial + 2 retries),
        // then after the re-plan swap, the single step on the new plan succeeds.
        var runner = new ScriptedStepRunner(
            Fail(0, "try 1"), Fail(0, "try 2"), Fail(0, "try 3"),
            Succeed(0));
        var exec = MakeExecutor(runner, new ExecutorOptions(MaxReplans: 3, MaxStepRetries: 2));
        var runCtx = MakeRunContext();

        var planV2 = MakePlan("p5", 2, MakeStep(0, "different approach"));
        ReplanTrigger? observedTrigger = null;
        Replanner replanner = (plan, outcomes, trigger, ct) =>
        {
            observedTrigger = trigger;
            return Task.FromResult(planV2);
        };

        var result = await exec.ExecuteAsync(planV1, runCtx, replanner);

        Assert.Equal(ExecutionTerminalState.Completed, result.TerminalState);
        Assert.Equal(1, result.ReplanCount);
        Assert.NotNull(observedTrigger);
        Assert.Equal(ReplanReason.RepeatedFailure, observedTrigger!.Kind);
        // 3 failed attempts + 1 succeed after re-plan = 4 outcomes.
        Assert.Equal(4, result.Outcomes.Count);
        Assert.Equal(new[] { (0, 1), (0, 2), (0, 3), (0, 1) }, runner.Calls);
    }

    // ── Re-plan budget ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ReplanBudgetExhausted_ReturnsFailedAfterReplans()
    {
        var plan = MakePlan("p6", 1, MakeStep(0));
        // Every run of step 0 contradicts, forcing a re-plan each time.
        // With MaxReplans=2 the executor should: run, replan, run, replan,
        // run, hit budget, terminate. That's 3 step runs + 2 re-plans.
        var runner = new ScriptedStepRunner(
            Contradict(0), Contradict(0), Contradict(0));
        var exec = MakeExecutor(runner, new ExecutorOptions(MaxReplans: 2, MaxStepRetries: 2));
        var runCtx = MakeRunContext();

        var replanCalls = 0;
        Replanner replanner = (p, o, t, ct) =>
        {
            replanCalls++;
            return Task.FromResult(MakePlan("p6", p.PlanVersion + 1, MakeStep(0)));
        };

        var result = await exec.ExecuteAsync(plan, runCtx, replanner);

        Assert.Equal(ExecutionTerminalState.FailedAfterReplans, result.TerminalState);
        Assert.Equal(2, result.ReplanCount);
        Assert.Equal(2, replanCalls);
        Assert.Equal(3, runner.Calls.Count);
    }

    // ── Cancellation ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Cancelled_ReturnsCancelledAndWritesRunCompleted()
    {
        var plan = MakePlan("p7", 1, MakeStep(0), MakeStep(1));
        using var cts = new CancellationTokenSource();
        var runner = new ScriptedStepRunner(new StepOutcome(
            StepIndex: 0, Status: StepStatus.Succeeded, Summary: "ok",
            ObservationJson: null, MatchedExpectation: true,
            StartedAt: DateTimeOffset.UtcNow, CompletedAt: DateTimeOffset.UtcNow));

        // Cancel after the first step completes, before the second step starts.
        // We do this by cancelling immediately; the loop checks the token at
        // the top of each iteration, so step 0 will still run but step 1 won't.
        // Use a custom runner that cancels after its first call.
        var runnerCancelling = new CancellingStepRunner(cts);
        var exec = MakeExecutor(runnerCancelling);
        var runCtx = MakeRunContext("run-cancel");

        var result = await exec.ExecuteAsync(
            plan, runCtx,
            (p, o, t, ct) => throw new Xunit.Sdk.XunitException("no replan expected"),
            cts.Token);

        Assert.Equal(ExecutionTerminalState.Cancelled, result.TerminalState);
        // Trace must end with a run_completed even on cancellation.
        var entries = await _traceStore.LoadAsync("run-cancel");
        Assert.Equal("run_completed", entries[^1].EntryType);
    }

    private sealed class CancellingStepRunner(CancellationTokenSource cts) : IStepRunner
    {
        public Task<StepOutcome> RunAsync(RuntimeStep step, StepExecutionContext context, CancellationToken ct)
        {
            // Let step 0 succeed, then cancel so step 1 never starts.
            cts.Cancel();
            return Task.FromResult(new StepOutcome(
                StepIndex: step.Index, Status: StepStatus.Succeeded, Summary: "ok",
                ObservationJson: null, MatchedExpectation: true,
                StartedAt: DateTimeOffset.UtcNow, CompletedAt: DateTimeOffset.UtcNow));
        }
    }

    // ── Step runner exceptions are converted to Failed outcomes ──────────

    [Fact]
    public async Task ExecuteAsync_StepRunnerThrows_ConvertsToFailedOutcomeAndRetries()
    {
        var plan = MakePlan("p8", 1, MakeStep(0));
        var runner = new ThrowingStepRunner(new InvalidOperationException("runner exploded"));
        var exec = MakeExecutor(runner, new ExecutorOptions(MaxReplans: 0, MaxStepRetries: 1));
        var runCtx = MakeRunContext();

        var replanner = new Replanner((p, o, t, ct) => Task.FromResult(p)); // never called — budget is 0

        var result = await exec.ExecuteAsync(plan, runCtx, replanner);

        // MaxReplans=0 means the executor cannot re-plan, so after retries
        // exhaust it must terminate FailedAfterReplans.
        Assert.Equal(ExecutionTerminalState.FailedAfterReplans, result.TerminalState);
        Assert.Equal(2, runner.CallCount); // 1 initial + 1 retry
        Assert.All(result.Outcomes, o =>
        {
            Assert.Equal(StepStatus.Failed, o.Status);
            Assert.Contains("runner exploded", o.ErrorMessage);
        });
    }
}
