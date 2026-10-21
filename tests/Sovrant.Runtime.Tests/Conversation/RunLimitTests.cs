using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Conversation;

/// <summary>
/// Phase 145 A8.1 — one time limit for any run (2 hours by default, set by admins). A workflow that
/// hits it is stopped and marked failed with the reason; a chat reply's own timeout pauses while it
/// waits for an approval, but the run limit still applies.
/// </summary>
public sealed class RunLimitTests : IAsyncDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_runlimit_{Guid.NewGuid():N}.db");
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteWorkflowStore _store;

    public RunLimitTests()
    {
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkflowStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }

    [Fact]
    public async Task The_Limit_Is_Two_Hours_Unless_An_Admin_Or_The_Env_Says_Otherwise()
    {
        var store = new MemorySettings();
        Assert.Equal(TimeSpan.FromHours(2), RunLimits.Load(store, _ => null));
        Assert.Equal(TimeSpan.FromMinutes(30), RunLimits.Load(store, k => k == RunLimits.MaxRunMinutesVariable ? "30" : null));

        await RunLimits.SaveAsync(store, 240);
        Assert.Equal(TimeSpan.FromHours(4), RunLimits.Load(store, k => k == RunLimits.MaxRunMinutesVariable ? "30" : null)); // admin wins
        Assert.Equal("Stopped after 4 hours — the time limit set by your admin. What finished before then is kept.",
            RunLimits.StoppedMessage(TimeSpan.FromHours(4)));
    }

    [Fact]
    public async Task A_Workflow_That_Runs_Past_The_Limit_Is_Stopped_And_Says_Why()
    {
        var wf = await _store.CreateAsync("a goal that takes forever", ownerUserId: "sam@example.com");
        await _store.UpdateStateAsync(wf.Id, WorkflowStatus.Running);
        var executor = new TimeLimitedWorkflowExecutor(new Forever(), _store, new MemorySettings(), clock: new InstantTimers());

        var result = await executor.RunAsync(wf.Id);

        Assert.Equal(WorkflowStatus.Failed, result.Status);
        var events = await _store.GetEventsAsync(wf.Id);
        Assert.Contains(events, e => e.EventType == WorkflowEventTypes.Failed && e.PayloadJson.Contains("time limit set by your admin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stopping_A_Workflow_Yourself_Is_Not_Reported_As_The_Limit()
    {
        var wf = await _store.CreateAsync("goal", ownerUserId: "sam@example.com");
        var executor = new TimeLimitedWorkflowExecutor(new Forever(), _store, new MemorySettings());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.RunAsync(wf.Id, cts.Token));
        Assert.DoesNotContain(await _store.GetEventsAsync(wf.Id), e => e.EventType == WorkflowEventTypes.Failed);
    }

    [Fact]
    public async Task Waiting_For_An_Approval_Pauses_The_Reply_Timeout_But_Not_The_Run_Limit()
    {
        using var cts = new CancellationTokenSource();
        using var deadline = RunDeadline.Begin(cts, turnTimeout: TimeSpan.FromMilliseconds(500), runLimit: TimeSpan.FromSeconds(30));

        using (RunDeadline.Current!.PauseForApproval())
        {
            await Task.Delay(1500);                      // three times the reply timeout, waiting for approval
            Assert.False(cts.IsCancellationRequested);
        }
        await WaitUntil(() => cts.IsCancellationRequested); // the reply timeout runs again after resuming
        Assert.False(deadline.RunLimitReached);          // it was the reply timeout, not the run limit
    }

    [Fact]
    public async Task The_Run_Limit_Still_Ends_A_Reply_That_Waits_For_An_Approval_Forever()
    {
        using var cts = new CancellationTokenSource();
        using var deadline = RunDeadline.Begin(cts, turnTimeout: TimeSpan.FromSeconds(60), runLimit: TimeSpan.FromMilliseconds(400));

        using (deadline.PauseForApproval())
            await WaitUntil(() => cts.IsCancellationRequested);

        Assert.True(deadline.RunLimitReached);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++) await Task.Delay(20);
        Assert.True(condition(), "timed out waiting");
    }

    private sealed class Forever : IWorkflowExecutor
    {
        public async Task<Workflow> RunAsync(string workflowId, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        }
    }

    /// <summary>Timers fire almost at once — stands in for "two hours later".</summary>
    private sealed class InstantTimers : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, dueTime == Timeout.InfiniteTimeSpan ? dueTime : TimeSpan.FromMilliseconds(20), period);
    }

    private sealed class MemorySettings : IWorkspaceSettingsStore
    {
        private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);
        public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default) => Task.FromResult(_data.TryGetValue(key, out var v) ? v : null);
        public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default) => GetGlobalAsync(key, ct);
        public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default) { _data[key] = value; return Task.CompletedTask; }
        public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default) { _data.Remove(key); return Task.CompletedTask; }
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_data, StringComparer.Ordinal));
    }
}
