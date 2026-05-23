using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;
using Sovrant.Agents.Templates;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Tools;

namespace Sovrant.Agents.Tests.Swarm;

/// <summary>
/// Phase 78 Path 2 — verifies that <see cref="SwarmOrchestrator"/> honors
/// <see cref="SwarmConfig.FileLocksEnabled"/> on both branches: when true,
/// a pre-held lock blocks the conflicting task; when false, the lock manager
/// is never consulted.
/// </summary>
public sealed class SwarmOrchestratorFileLockTests
{
    [Fact]
    public async Task FileLocksEnabled_PreHeldLock_BlocksConflictingTask()
    {
        var locks = new SpyFileLockManager();
        locks.TryAcquire("report.md", "external-task");

        var orchestrator = BuildOrchestrator(locks);

        var plan = new SwarmPlan
        {
            Id = "swarm-lock-block",
            OriginalPrompt = "write a report",
            Tasks =
            [
                new SwarmTaskNode
                {
                    Id = "t1",
                    Description = "write report",
                    Wave = 0,
                    FilesToModify = { "report.md" },
                },
            ],
            WaveCount = 1,
        };

        var result = await orchestrator.ExecuteAsync(
            plan,
            new SwarmConfig { Enabled = true, FileLocksEnabled = true, MaxConcurrent = 1 });

        // Task was blocked on the pre-held lock — never reaches CreateAgent.
        Assert.Equal(SwarmTaskStatus.Blocked, plan.Tasks[0].Status);
        Assert.Contains("locked by", plan.Tasks[0].Error!, StringComparison.Ordinal);
        Assert.True(locks.IsLockedByOtherCalls > 0, "IsLockedByOther must be consulted when locks are enabled.");
        _ = result; // SwarmOrchestrator treats Blocked as a non-failure for the overall run status.
    }

    [Fact]
    public async Task FileLocksDisabled_LockManagerIsNeverConsulted()
    {
        var locks = new SpyFileLockManager();
        // Pre-seed an "external" lock that *would* block if locks were enabled.
        locks.TryAcquire("report.md", "external-task");
        var baselineIsLockedCalls = locks.IsLockedByOtherCalls;
        var baselineTryAcquireCalls = locks.TryAcquireCalls;

        var orchestrator = BuildOrchestrator(locks);

        var plan = new SwarmPlan
        {
            Id = "swarm-lock-off",
            OriginalPrompt = "write a report",
            Tasks =
            [
                new SwarmTaskNode
                {
                    Id = "t1",
                    Description = "write report",
                    Wave = 0,
                    FilesToModify = { "report.md" },
                },
            ],
            WaveCount = 1,
        };

        // With locks disabled the task will skip the lock block and try to
        // create an agent. We don't care that agent creation fails below —
        // the assertions here are only about whether the lock manager was
        // touched during the lock-check phase.
        try
        {
            await orchestrator.ExecuteAsync(
                plan,
                new SwarmConfig { Enabled = true, FileLocksEnabled = false, MaxConcurrent = 1 });
        }
        catch
        {
            // Agent-creation failures are expected with our stubbed factory.
        }

        Assert.Equal(baselineIsLockedCalls, locks.IsLockedByOtherCalls);
        Assert.Equal(baselineTryAcquireCalls, locks.TryAcquireCalls);
        // Task must NOT have been Blocked by the lock check.
        Assert.NotEqual(SwarmTaskStatus.Blocked, plan.Tasks[0].Status);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static SwarmOrchestrator BuildOrchestrator(IFileLockManager locks)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new SovrantAgentFactory(services, NullLoggerFactory.Instance);
        var templates = new AgentTemplateRegistry(NullLogger<AgentTemplateRegistry>.Instance);
        var teamRegistry = new InMemoryTeamRegistry();
        var stateTracker = new SwarmStateTracker();
        var session = new SwarmSession(new FakeSwarmEventStore());
        var toolRegistry = new InMemoryToolRegistry();
        var workspace = new WorkspaceContext();

        return new SwarmOrchestrator(
            factory,
            templates,
            teamRegistry,
            locks,
            stateTracker,
            session,
            toolRegistry,
            workspace,
            NullLogger<SwarmOrchestrator>.Instance);
    }

    /// <summary>Wraps <see cref="FileLockManager"/> and counts method invocations.</summary>
    private sealed class SpyFileLockManager : IFileLockManager
    {
        private readonly FileLockManager _inner = new();
        public int IsLockedByOtherCalls { get; private set; }
        public int TryAcquireCalls { get; private set; }
        public int ReleaseAllCalls { get; private set; }
        public int ClearCalls { get; private set; }

        public bool TryAcquire(string filePath, string taskId)
        {
            TryAcquireCalls++;
            return _inner.TryAcquire(filePath, taskId);
        }

        public bool IsLockedByOther(string filePath, string taskId)
        {
            IsLockedByOtherCalls++;
            return _inner.IsLockedByOther(filePath, taskId);
        }

        public string? GetHolder(string filePath) => _inner.GetHolder(filePath);

        public void ReleaseAll(string taskId)
        {
            ReleaseAllCalls++;
            _inner.ReleaseAll(taskId);
        }

        public void Clear()
        {
            ClearCalls++;
            _inner.Clear();
        }

        public IReadOnlyDictionary<string, string> Snapshot() => _inner.Snapshot();
    }

    private sealed class FakeSwarmEventStore : ISwarmEventStore
    {
        public Task RecordEventAsync(SwarmEventRecord record, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SwarmEventRecord>> LoadEventsAsync(string swarmId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SwarmEventRecord>>([]);

        public Task<IReadOnlyList<string>> ListSwarmsAsync(SwarmListFilter? filter = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<bool> ExistsAsync(string swarmId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<int> DeleteSwarmAsync(string swarmId, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task<string?> GetOwnerAsync(string swarmId, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyList<string>> ListChildrenAsync(string parentSwarmId, int? limit = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
