using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Orchestration;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;
using Sovrant.Agents.Templates;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Tools;

namespace Sovrant.Agents.Tests.Orchestration;

/// <summary>
/// Phase 78 Path 2 commit 10 — end-to-end integration test.
/// Wires a real <see cref="AgentOrchestrator"/> to a real <see cref="SwarmOrchestrator"/>
/// (not a capturing stub) with a real <see cref="FileLockManager"/>, then proves
/// a team's run profile — Parallel, MaxConcurrent, FileLocksEnabled — actually
/// shapes what happens at the swarm layer. Conflicting writes in the same wave
/// must end with <see cref="SwarmTaskStatus.Blocked"/> when file locks are enabled,
/// and must not when locks are off.
/// </summary>
public sealed class AgentOrchestratorTeamRunIntegrationTests
{
    [Fact]
    public async Task ParallelTeam_WithFileLocks_BlocksConflictingTasksEndToEnd()
    {
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-integ-locks",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "integ-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            RunMode = TeamRunMode.Parallel,
            MaxConcurrent = 4,
            FileLocksEnabled = true,
            DecompositionMode = TeamDecompositionMode.Off,
        };
        registry.CreateTeam(team);

        var locks = new FileLockManager();
        // Pre-hold the lock with an external task so every plan task hits the
        // conflict branch — proves the lock manager is actually reached via
        // the full AgentOrchestrator → SwarmOrchestrator pipeline.
        locks.TryAcquire("report.md", "external-holder");

        var swarm = BuildRealSwarmOrchestrator(registry, locks);
        var orchestrator = new AgentOrchestrator(
            swarm,
            new UnreachableDecomposer(),   // Plan is supplied → decomposer must not be called.
            registry,
            new NoOpAgentRunStore(),
            new PassingQualityGate(),
            new SwarmConfig { Enabled = true, FileLocksEnabled = false }, // global default says OFF
            NullLogger<AgentOrchestrator>.Instance);

        var planTasks = new List<SwarmTaskNode>
        {
            new() { Id = "t1", Description = "first writer", Wave = 0, FilesToModify = { "report.md" } },
            new() { Id = "t2", Description = "second writer", Wave = 0, FilesToModify = { "report.md" } },
            new() { Id = "t3", Description = "third writer", Wave = 0, FilesToModify = { "report.md" } },
        };

        var result = await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "write report",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
            Plan = planTasks,
        });

        Assert.All(planTasks, t => Assert.Equal(SwarmTaskStatus.Blocked, t.Status));
        Assert.All(planTasks, t => Assert.Contains("locked by", t.Error!, StringComparison.Ordinal));
        // SwarmOrchestrator treats Blocked as a non-failure, so run status is Completed.
        Assert.Equal(SwarmStatus.Completed, result.SwarmResult.Status);
    }

    [Fact]
    public async Task ParallelTeam_WithFileLocksDisabled_DoesNotBlockOnPreHeldLock()
    {
        // Same topology as the previous test, but the team explicitly turns
        // file locks off. The pre-held external lock must be ignored — no task
        // should end up Blocked, proving FileLocksEnabled=false from the team
        // profile actually reaches the real SwarmOrchestrator.
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-integ-nolocks",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "nolocks-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            RunMode = TeamRunMode.Parallel,
            MaxConcurrent = 4,
            FileLocksEnabled = false,
            DecompositionMode = TeamDecompositionMode.Off,
        };
        registry.CreateTeam(team);

        var locks = new FileLockManager();
        locks.TryAcquire("report.md", "external-holder");

        var swarm = BuildRealSwarmOrchestrator(registry, locks);
        var orchestrator = new AgentOrchestrator(
            swarm,
            new UnreachableDecomposer(),
            registry,
            new NoOpAgentRunStore(),
            new PassingQualityGate(),
            new SwarmConfig { Enabled = true, FileLocksEnabled = true }, // global default says ON
            NullLogger<AgentOrchestrator>.Instance);

        var planTasks = new List<SwarmTaskNode>
        {
            new() { Id = "t1", Description = "writer", Wave = 0, FilesToModify = { "report.md" } },
        };

        // With locks off, the task sails past the lock check and tries to build
        // a real agent — which our stub DI can't satisfy. That's expected:
        // the test is only asserting the lock check didn't fire.
        try
        {
            await orchestrator.RunAsync(new EnsembleRunRequest
            {
                Goal = "write report",
                TeamId = team.Id,
                WorkspaceId = "ws-1",
                UserId = "alice",
                Plan = planTasks,
            });
        }
        catch
        {
            // Agent-creation failure from stub factory — expected.
        }

        Assert.NotEqual(SwarmTaskStatus.Blocked, planTasks[0].Status);
    }

    [Fact]
    public async Task SequentialTeam_ForcesMaxConcurrentToOne_ThroughRealSwarmPipeline()
    {
        // Sequential teams must emit MaxConcurrent=1 into the real SwarmConfig
        // that reaches the SwarmOrchestrator, regardless of what the team's
        // MaxConcurrent field says. Use the pre-held-lock trick to observe the
        // effective config without needing real agent execution: if MaxConcurrent
        // plumbing works and locks are on, both tasks still get Blocked.
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-integ-seq",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "seq-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            RunMode = TeamRunMode.Sequential,
            MaxConcurrent = 9, // should be clamped to 1 by Sequential mode
            FileLocksEnabled = true,
            DecompositionMode = TeamDecompositionMode.Off,
        };
        registry.CreateTeam(team);

        var locks = new FileLockManager();
        locks.TryAcquire("report.md", "external-holder");

        // Capture the effective config that reaches the swarm layer while also
        // running the real block-on-lock code path via a delegating orchestrator.
        var real = BuildRealSwarmOrchestrator(registry, locks);
        var capturing = new ConfigCapturingSwarm(real);

        var orchestrator = new AgentOrchestrator(
            capturing,
            new UnreachableDecomposer(),
            registry,
            new NoOpAgentRunStore(),
            new PassingQualityGate(),
            new SwarmConfig { Enabled = true, MaxConcurrent = 16 },
            NullLogger<AgentOrchestrator>.Instance);

        var planTasks = new List<SwarmTaskNode>
        {
            new() { Id = "t1", Description = "writer", Wave = 0, FilesToModify = { "report.md" } },
        };

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "sequential write",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
            Plan = planTasks,
        });

        Assert.NotNull(capturing.LastConfig);
        Assert.Equal(1, capturing.LastConfig!.MaxConcurrent);
        Assert.True(capturing.LastConfig.FileLocksEnabled);
        Assert.Equal(SwarmTaskStatus.Blocked, planTasks[0].Status);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static SwarmOrchestrator BuildRealSwarmOrchestrator(
        ITeamRegistry registry,
        IFileLockManager locks)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new SovrantAgentFactory(services, NullLoggerFactory.Instance);
        var templates = new AgentTemplateRegistry(NullLogger<AgentTemplateRegistry>.Instance);
        var stateTracker = new SwarmStateTracker();
        var session = new SwarmSession(new NoOpSwarmEventStore());
        var toolRegistry = new InMemoryToolRegistry();
        var workspace = new WorkspaceContext();

        return new SwarmOrchestrator(
            factory,
            templates,
            registry,
            locks,
            stateTracker,
            session,
            toolRegistry,
            workspace,
            NullLogger<SwarmOrchestrator>.Instance);
    }

    /// <summary>Delegates to an inner orchestrator but captures the config it received.</summary>
    private sealed class ConfigCapturingSwarm : ISwarmOrchestrator
    {
        private readonly ISwarmOrchestrator _inner;

        public ConfigCapturingSwarm(ISwarmOrchestrator inner) { _inner = inner; }

        public SwarmConfig? LastConfig { get; private set; }

        public Task<SwarmResult> ExecuteAsync(
            SwarmPlan plan,
            SwarmConfig config,
            Action<SwarmEvent>? onEvent = null,
            SwarmExecutionContext? executionContext = null,
            CancellationToken ct = default)
        {
            LastConfig = config;
            return _inner.ExecuteAsync(plan, config, onEvent, executionContext, ct);
        }
    }

    /// <summary>Decomposer that must never be called — fails loudly if it is.</summary>
    private sealed class UnreachableDecomposer : ISwarmDecomposer
    {
        public Task<SwarmPlan> DecomposeAsync(string prompt, SwarmConfig config, CancellationToken ct = default) =>
            throw new InvalidOperationException("Decomposer was invoked despite a pre-built plan being supplied.");
    }

    private sealed class PassingQualityGate : ISwarmQualityGate
    {
        public Task<QualityVerdict> ReviewAsync(string swarmId, string originalPrompt, string combinedOutput, CancellationToken ct = default) =>
            Task.FromResult(new QualityVerdict(10, "pass", "ok"));
    }

    private sealed class NoOpAgentRunStore : IAgentRunStore
    {
        public Task<AgentRunRecord> CreateAsync(AgentRunRecord run, CancellationToken ct = default) =>
            Task.FromResult(run);

        public Task<AgentRunRecord?> GetAsync(string runId, CancellationToken ct = default) =>
            Task.FromResult<AgentRunRecord?>(null);

        public Task UpdateStatusAsync(string runId, string status, int inputTokens = 0, int outputTokens = 0, decimal? costUsd = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AgentRunRecord>> ListAsync(AgentRunFilter? filter = null, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AgentRunRecord>>([]);
    }

    private sealed class NoOpSwarmEventStore : ISwarmEventStore
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

    /// <summary>Minimal in-memory registry that supports team-level operations.</summary>
    private sealed class FakeTeamRegistry : ITeamRegistry
    {
        private readonly Dictionary<string, TeamInfo> _teams = [];
        private readonly Dictionary<string, TeamMemberInfo> _members = [];

        public string CreateTeam(TeamInfo team) { _teams[team.Id] = team; return team.Id; }
        public TeamInfo? GetTeam(string teamId) => _teams.TryGetValue(teamId, out var t) ? t : null;
        public IReadOnlyList<TeamInfo> ListTeams(string? workspaceId = null) =>
            _teams.Values.Where(t => workspaceId is null || t.WorkspaceId == workspaceId).ToList();
        public bool RemoveTeam(string teamId) => _teams.Remove(teamId);
        public IReadOnlyList<TeamMemberInfo> GetTeamMembers(string teamId) =>
            _members.Values.Where(m => m.TeamId == teamId).ToList();

        public string RegisterMember(TeamMemberInfo member) { _members[member.Id] = member; return member.Id; }
        public bool RemoveMember(string memberId) => _members.Remove(memberId);
        public TeamMemberInfo? GetMember(string memberId) =>
            _members.TryGetValue(memberId, out var m) ? m : null;
        public IReadOnlyList<TeamMemberInfo> GetAllMembers() => _members.Values.ToList();
    }
}
