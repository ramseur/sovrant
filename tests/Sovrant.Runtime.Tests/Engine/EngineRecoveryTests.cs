using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Engine;

public sealed class EngineRecoveryTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IRuntimeTraceStore _traceStore;
    private readonly EngineRecovery _recovery;

    public EngineRecoveryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_recovery_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _traceStore = new SqliteRuntimeTraceStore(_provider);
        _recovery = new EngineRecovery(_traceStore, NullLogger<EngineRecovery>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private async Task AppendAsync(
        string runId,
        string entryType,
        int? stepIndex,
        string planId = "plan-1",
        int planVersion = 1,
        string? sessionId = "sess-1",
        string? workspaceId = "ws-1",
        string? projectId = "proj-1")
    {
        await _traceStore.AppendAsync(new RuntimeTraceEntry(
            RuntimeRunId: runId,
            PlanId: planId,
            PlanVersion: planVersion,
            StepIndex: stepIndex,
            EntryType: entryType,
            Payload: "{}",
            Timestamp: DateTimeOffset.UtcNow,
            SessionId: sessionId,
            WorkspaceId: workspaceId,
            ProjectId: projectId));
    }

    [Fact]
    public async Task Recover_NoInFlightRuns_ReturnsEmpty()
    {
        var result = await _recovery.RecoverAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task Recover_ClosesOrphanedStepStarted()
    {
        // Simulate a run that got through plan_created and step_started for
        // step 0, then crashed before writing step_completed.
        await AppendAsync("run-crash", "plan_created", null);
        await AppendAsync("run-crash", "step_started", 0);

        // Sanity: the store sees this as in-flight.
        var inFlightBefore = await _traceStore.ListInFlightRunsAsync();
        Assert.Contains("run-crash", inFlightBefore);

        var recovered = await _recovery.RecoverAsync();

        var run = Assert.Single(recovered);
        Assert.Equal("run-crash", run.RuntimeRunId);
        Assert.Equal("plan-1", run.PlanId);
        Assert.Equal(1, run.PlanVersion);
        Assert.Equal(new[] { 0 }, run.OrphanedStepIndexes);
        Assert.Equal("sess-1", run.SessionId);
        Assert.Equal("ws-1", run.WorkspaceId);
        Assert.Equal("proj-1", run.ProjectId);

        // After recovery the run must no longer be in-flight.
        var inFlightAfter = await _traceStore.ListInFlightRunsAsync();
        Assert.DoesNotContain("run-crash", inFlightAfter);

        // Trace should now end with the synthesised step_failed + run_completed.
        var entries = await _traceStore.LoadAsync("run-crash");
        Assert.Equal("step_failed", entries[^2].EntryType);
        Assert.Equal("run_completed", entries[^1].EntryType);
    }

    [Fact]
    public async Task Recover_IgnoresCleanlyCompletedRuns()
    {
        await AppendAsync("run-clean", "plan_created", null);
        await AppendAsync("run-clean", "step_started", 0);
        await AppendAsync("run-clean", "step_completed", 0);
        await AppendAsync("run-clean", "run_completed", null);

        var result = await _recovery.RecoverAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task Recover_HandlesMultipleOrphanedStepsInOneRun()
    {
        // A run with two orphaned step_started rows on different plan
        // versions (e.g. crashed during a re-plan).
        await AppendAsync("run-multi", "plan_created", null, planVersion: 1);
        await AppendAsync("run-multi", "step_started", 0, planVersion: 1);
        await AppendAsync("run-multi", "step_started", 1, planVersion: 1);

        var recovered = await _recovery.RecoverAsync();

        var run = Assert.Single(recovered);
        Assert.Equal(new[] { 0, 1 }, run.OrphanedStepIndexes);

        var inFlight = await _traceStore.ListInFlightRunsAsync();
        Assert.Empty(inFlight);
    }

    [Fact]
    public async Task Recover_HandlesMultipleInFlightRuns()
    {
        await AppendAsync("run-a", "step_started", 0);
        await AppendAsync("run-b", "step_started", 0);

        var recovered = await _recovery.RecoverAsync();

        Assert.Equal(2, recovered.Count);
        Assert.Contains(recovered, r => r.RuntimeRunId == "run-a");
        Assert.Contains(recovered, r => r.RuntimeRunId == "run-b");
    }

    [Fact]
    public async Task Recover_IsIdempotent()
    {
        await AppendAsync("run-idem", "step_started", 0);

        var first = await _recovery.RecoverAsync();
        Assert.Single(first);

        var second = await _recovery.RecoverAsync();
        Assert.Empty(second);
    }

    [Fact]
    public async Task Recover_DistinguishesByPlanVersion()
    {
        // step_started at version 1 was completed, but version 2 crashed.
        await AppendAsync("run-versions", "step_started", 0, planVersion: 1);
        await AppendAsync("run-versions", "step_completed", 0, planVersion: 1);
        await AppendAsync("run-versions", "step_started", 0, planVersion: 2);

        var recovered = await _recovery.RecoverAsync();

        var run = Assert.Single(recovered);
        Assert.Equal(2, run.PlanVersion);
        Assert.Equal(new[] { 0 }, run.OrphanedStepIndexes);
    }
}
