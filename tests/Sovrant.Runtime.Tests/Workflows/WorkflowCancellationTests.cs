using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Runtime.Tests.Workflows;

/// <summary>
/// Phase 148 — cancelling a workflow (Workflows page, API, /workflow cancel) used to only change its
/// status: the run carried on, kept using the model, and wrote its own status over the cancel
/// (Running again, Completed, Failed). Now the cancel stops the run, and cancelled stays cancelled.
/// </summary>
public sealed class WorkflowCancellationTests : IAsyncDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_wfcancel_{Guid.NewGuid():N}.db");
    private readonly SqliteStorageProvider _storage;
    private readonly SqliteWorkflowStore _store;

    public WorkflowCancellationTests()
    {
        _storage = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _storage.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkflowStore(_storage);
    }

    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp file */ }
    }

    [Fact]
    public async Task Cancelling_A_Running_Workflow_Stops_Its_Run_And_It_Stays_Cancelled()
    {
        var wf = await _store.CreateAsync("long goal", ownerUserId: "sam@example.com");
        var inner = new RunsUntilStopped(_store);
        var executor = new TimeLimitedWorkflowExecutor(inner, _store, settings: null);

        var run = executor.RunAsync(wf.Id);
        await inner.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(await _store.CancelAsync((await _store.GetAsync(wf.Id))!));

        var result = await run.WaitAsync(TimeSpan.FromSeconds(10)); // the run really stopped
        Assert.True(inner.SawCancellation);
        Assert.Equal(WorkflowStatus.Cancelled, result.Status);
        Assert.Equal(WorkflowStatus.Cancelled, (await _store.GetAsync(wf.Id))!.Status);
        Assert.False(WorkflowRuns.IsRunning(wf.Id));
    }

    [Theory]
    [InlineData(WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Completed)]
    [InlineData(WorkflowStatus.Failed)]
    [InlineData(WorkflowStatus.AwaitingHuman)]
    public async Task A_Late_Status_Write_Cannot_Undo_A_Cancel(WorkflowStatus late)
    {
        var wf = await _store.CreateAsync("goal", ownerUserId: "sam@example.com");
        await _store.UpdateStateAsync(wf.Id, WorkflowStatus.Running);
        await _store.CancelAsync((await _store.GetAsync(wf.Id))!);

        await _store.UpdateStateAsync(wf.Id, late, planJson: "{\"steps\":[]}"); // e.g. a run in another process

        var now = (await _store.GetAsync(wf.Id))!;
        Assert.Equal(WorkflowStatus.Cancelled, now.Status);
    }

    [Fact]
    public async Task Other_Status_Changes_Still_Work()
    {
        var wf = await _store.CreateAsync("goal", ownerUserId: "sam@example.com");
        await _store.UpdateStateAsync(wf.Id, WorkflowStatus.Running);
        await _store.UpdateStateAsync(wf.Id, WorkflowStatus.Completed);
        Assert.Equal(WorkflowStatus.Completed, (await _store.GetAsync(wf.Id))!.Status);
    }

    /// <summary>Marks the workflow running, then works until its run is stopped.</summary>
    private sealed class RunsUntilStopped(SqliteWorkflowStore store) : IWorkflowExecutor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool SawCancellation { get; private set; }

        public async Task<Workflow> RunAsync(string workflowId, CancellationToken ct = default)
        {
            await store.UpdateStateAsync(workflowId, WorkflowStatus.Running, ct: ct);
            Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { SawCancellation = true; throw; }
            throw new InvalidOperationException("unreachable");
        }
    }
}
