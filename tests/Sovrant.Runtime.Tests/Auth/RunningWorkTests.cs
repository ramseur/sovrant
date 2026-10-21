using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Runtime.Tests.Auth;

/// <summary>
/// Phase 145 A8.3 — "Sign out everywhere", an admin revoke and disabling an account stop that
/// person's running work; an ordinary sign-out doesn't (work outlives it). Real SQLite.
/// </summary>
public sealed class RunningWorkTests : IAsyncDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_running_{Guid.NewGuid():N}.db");
    private readonly SqliteStorageProvider _storage;
    private readonly SqliteWebSignInService _signIns;
    private readonly SqliteUserStore _users;
    private readonly string _user = $"sam-{Guid.NewGuid():N}@example.com"; // unique: the registry is process-wide

    public RunningWorkTests()
    {
        _storage = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _storage.InitializeAsync().GetAwaiter().GetResult();
        _users = new SqliteUserStore(_storage, NullLogger<SqliteUserStore>.Instance);
        _users.CreateAsync(userId: _user).GetAwaiter().GetResult();
        _signIns = new SqliteWebSignInService(_storage);
    }

    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }

    [Fact]
    public async Task An_Ordinary_Sign_Out_Leaves_Work_Running()
    {
        var (signIn, _) = await _signIns.CreateAsync(_user, remember: false, null, null, WebSignInPolicy.Default);
        using var cts = new CancellationTokenSource();
        using var run = RunningWork.Track(_user, cts);

        await _signIns.RevokeAsync(signIn.SignInId, WebSignInRevokeReasons.SignOut);

        Assert.False(cts.IsCancellationRequested);
        Assert.False(run.StoppedBySignOut);
    }

    [Theory]
    [InlineData(WebSignInRevokeReasons.SignOutEverywhere)]
    [InlineData(WebSignInRevokeReasons.Admin)]
    public async Task Sign_Out_Everywhere_And_Admin_Sign_Out_Stop_Running_Work(string reason)
    {
        await _signIns.CreateAsync(_user, remember: false, null, null, WebSignInPolicy.Default);
        using var cts = new CancellationTokenSource();
        using var run = RunningWork.Track(_user, cts);
        using var someoneElse = new CancellationTokenSource();
        using var other = RunningWork.Track($"other-{Guid.NewGuid():N}@example.com", someoneElse);

        await _signIns.RevokeAllAsync(_user, reason);

        Assert.True(cts.IsCancellationRequested);
        Assert.True(run.StoppedBySignOut);
        Assert.False(someoneElse.IsCancellationRequested); // only that person's work
    }

    [Fact]
    public async Task An_Admin_Revoking_One_Browser_Stops_Their_Work()
    {
        var (signIn, _) = await _signIns.CreateAsync(_user, remember: false, null, null, WebSignInPolicy.Default);
        using var cts = new CancellationTokenSource();
        using var run = RunningWork.Track(_user, cts);

        await _signIns.RevokeAsync(signIn.SignInId, WebSignInRevokeReasons.Admin);

        Assert.True(run.StoppedBySignOut);
    }

    [Fact]
    public async Task Disabling_An_Account_Stops_Its_Work()
    {
        using var cts = new CancellationTokenSource();
        using var run = RunningWork.Track(_user, cts);

        Assert.True(await _users.DeactivateAsync(_user));

        Assert.True(run.StoppedBySignOut);
    }

    [Fact]
    public async Task A_Workflow_Stopped_By_A_Sign_Out_Is_Cancelled_And_Says_Why()
    {
        var store = new SqliteWorkflowStore(_storage);
        var wf = await store.CreateAsync("long goal", ownerUserId: _user);
        await store.UpdateStateAsync(wf.Id, WorkflowStatus.Running);
        var executor = new TimeLimitedWorkflowExecutor(new Forever(), store, settings: null);

        var running = executor.RunAsync(wf.Id);
        await WaitUntil(() => RunningWork.CountFor(_user) > 0);
        RunningWork.StopAllFor(_user);
        var result = await running;

        Assert.Equal(WorkflowStatus.Cancelled, result.Status);
        Assert.Contains(await store.GetEventsAsync(wf.Id),
            e => e.EventType == WorkflowEventTypes.Cancelled && e.PayloadJson.Contains("signed out everywhere", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Finished_Run_Is_Forgotten()
    {
        using var cts = new CancellationTokenSource();
        RunningWork.Track(_user, cts).Dispose();
        Assert.Equal(0, RunningWork.CountFor(_user));
        Assert.Equal(0, RunningWork.StopAllFor(_user));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class Forever : IWorkflowExecutor
    {
        public async Task<Workflow> RunAsync(string workflowId, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        }
    }
}
