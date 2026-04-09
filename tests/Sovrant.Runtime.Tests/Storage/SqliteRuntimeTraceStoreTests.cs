using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteRuntimeTraceStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IRuntimeTraceStore _store;

    public SqliteRuntimeTraceStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_runtime_trace_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteRuntimeTraceStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private static RuntimeTraceEntry MakeEntry(
        string runtimeRunId,
        string entryType = "step_started",
        int? stepIndex = 0,
        string planId = "plan-1",
        int planVersion = 1,
        string payload = """{"intent":"confirm file exists"}""",
        string? sessionId = null,
        string? workspaceId = null,
        string? projectId = null) =>
        new(
            RuntimeRunId: runtimeRunId,
            PlanId: planId,
            PlanVersion: planVersion,
            StepIndex: stepIndex,
            EntryType: entryType,
            Payload: payload,
            Timestamp: DateTimeOffset.UtcNow,
            SessionId: sessionId,
            WorkspaceId: workspaceId,
            ProjectId: projectId);

    [Fact]
    public async Task AppendAndLoad_RoundTripsAllFields()
    {
        var entry = MakeEntry(
            runtimeRunId: "run-1",
            entryType: "step_completed",
            stepIndex: 2,
            planId: "plan-abc",
            planVersion: 3,
            payload: """{"summary":"grepped 4 matches","matched":true}""",
            sessionId: "sess-1",
            workspaceId: "ws-personal-alice",
            projectId: "proj-1");

        await _store.AppendAsync(entry);

        var loaded = await _store.LoadAsync("run-1");

        var single = Assert.Single(loaded);
        Assert.Equal("run-1", single.RuntimeRunId);
        Assert.Equal("plan-abc", single.PlanId);
        Assert.Equal(3, single.PlanVersion);
        Assert.Equal(2, single.StepIndex);
        Assert.Equal("step_completed", single.EntryType);
        Assert.Equal("""{"summary":"grepped 4 matches","matched":true}""", single.Payload);
        Assert.Equal("sess-1", single.SessionId);
        Assert.Equal("ws-personal-alice", single.WorkspaceId);
        Assert.Equal("proj-1", single.ProjectId);
    }

    [Fact]
    public async Task AppendAndLoad_NullStepIndexRoundTrips()
    {
        // plan_created / run_completed entries are plan-level and have no step_index.
        var entry = MakeEntry("run-2", entryType: "plan_created", stepIndex: null);
        await _store.AppendAsync(entry);

        var loaded = await _store.LoadAsync("run-2");
        var single = Assert.Single(loaded);
        Assert.Null(single.StepIndex);
        Assert.Equal("plan_created", single.EntryType);
    }

    [Fact]
    public async Task Load_ReturnsEntriesInInsertionOrder()
    {
        await _store.AppendAsync(MakeEntry("run-3", entryType: "plan_created", stepIndex: null));
        await _store.AppendAsync(MakeEntry("run-3", entryType: "step_started", stepIndex: 0));
        await _store.AppendAsync(MakeEntry("run-3", entryType: "step_completed", stepIndex: 0));
        await _store.AppendAsync(MakeEntry("run-3", entryType: "step_started", stepIndex: 1));

        var loaded = await _store.LoadAsync("run-3");

        Assert.Equal(4, loaded.Count);
        Assert.Equal("plan_created", loaded[0].EntryType);
        Assert.Equal("step_started", loaded[1].EntryType);
        Assert.Equal("step_completed", loaded[2].EntryType);
        Assert.Equal("step_started", loaded[3].EntryType);
    }

    [Fact]
    public async Task Load_IsolatesByRuntimeRunId()
    {
        await _store.AppendAsync(MakeEntry("run-a", stepIndex: 0));
        await _store.AppendAsync(MakeEntry("run-b", stepIndex: 0));
        await _store.AppendAsync(MakeEntry("run-a", stepIndex: 1));

        var a = await _store.LoadAsync("run-a");
        var b = await _store.LoadAsync("run-b");

        Assert.Equal(2, a.Count);
        Assert.Single(b);
        Assert.All(a, e => Assert.Equal("run-a", e.RuntimeRunId));
        Assert.All(b, e => Assert.Equal("run-b", e.RuntimeRunId));
    }

    [Fact]
    public async Task ListInFlightRuns_ReturnsRunWithUnmatchedStepStarted()
    {
        // run-x started step 0, completed step 0, started step 1 but never
        // finished → in-flight.
        await _store.AppendAsync(MakeEntry("run-x", entryType: "step_started", stepIndex: 0));
        await _store.AppendAsync(MakeEntry("run-x", entryType: "step_completed", stepIndex: 0));
        await _store.AppendAsync(MakeEntry("run-x", entryType: "step_started", stepIndex: 1));

        var inFlight = await _store.ListInFlightRunsAsync();

        Assert.Contains("run-x", inFlight);
    }

    [Fact]
    public async Task ListInFlightRuns_ExcludesCleanlyFinishedRuns()
    {
        await _store.AppendAsync(MakeEntry("run-y", entryType: "step_started", stepIndex: 0));
        await _store.AppendAsync(MakeEntry("run-y", entryType: "step_completed", stepIndex: 0));
        await _store.AppendAsync(MakeEntry("run-y", entryType: "step_started", stepIndex: 1));
        await _store.AppendAsync(MakeEntry("run-y", entryType: "step_failed", stepIndex: 1));

        var inFlight = await _store.ListInFlightRunsAsync();

        Assert.DoesNotContain("run-y", inFlight);
    }

    [Fact]
    public async Task ListInFlightRuns_DistinguishesByPlanVersion()
    {
        // A re-plan reuses step_index 0 under a new plan_version. A v1 that
        // was cleanly finished must not mask a v2 that is in-flight.
        await _store.AppendAsync(MakeEntry("run-z", entryType: "step_started", stepIndex: 0, planVersion: 1));
        await _store.AppendAsync(MakeEntry("run-z", entryType: "step_completed", stepIndex: 0, planVersion: 1));
        await _store.AppendAsync(MakeEntry("run-z", entryType: "step_started", stepIndex: 0, planVersion: 2));

        var inFlight = await _store.ListInFlightRunsAsync();

        Assert.Contains("run-z", inFlight);
    }

    [Fact]
    public async Task DeleteRun_RemovesAllEntriesForRun()
    {
        await _store.AppendAsync(MakeEntry("run-del", stepIndex: 0));
        await _store.AppendAsync(MakeEntry("run-del", stepIndex: 1));
        await _store.AppendAsync(MakeEntry("run-keep", stepIndex: 0));

        var deleted = await _store.DeleteRunAsync("run-del");

        Assert.Equal(2, deleted);
        Assert.Empty(await _store.LoadAsync("run-del"));
        Assert.Single(await _store.LoadAsync("run-keep"));
    }
}
