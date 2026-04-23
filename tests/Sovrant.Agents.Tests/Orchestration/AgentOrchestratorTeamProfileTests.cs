using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Orchestration;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Tests.Orchestration;

/// <summary>
/// Phase 78 Path 2 — verifies that <see cref="AgentOrchestrator"/> overlays a
/// team's persisted run profile onto the effective <see cref="SwarmConfig"/>
/// that reaches the <see cref="ISwarmOrchestrator"/>.
/// </summary>
public sealed class AgentOrchestratorTeamProfileTests
{
    [Fact]
    public async Task TeamRunProfile_DrivesEffectiveSwarmConfig_WhenRequestFlagsAreOmitted()
    {
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-profile-1",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "profile-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            RunMode = TeamRunMode.Parallel,
            MaxConcurrent = 3,
            FileLocksEnabled = true,
            QualityGateEnabled = true,
            QualityGateThreshold = 8,
            DecompositionMode = TeamDecompositionMode.RoleAware,
        };
        registry.CreateTeam(team);

        var capturing = new CapturingSwarmOrchestrator();
        var decomposer = new StubDecomposer();
        var orchestrator = BuildOrchestrator(registry, capturing, decomposer);

        var result = await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "do the thing",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        Assert.NotNull(capturing.LastConfig);
        Assert.Equal(3, capturing.LastConfig!.MaxConcurrent);
        Assert.True(capturing.LastConfig.FileLocksEnabled);
        Assert.True(capturing.LastConfig.QualityGateEnabled);
        // RoleAware decomposition → decomposer must be invoked.
        Assert.Equal(1, decomposer.CallCount);
        Assert.Equal(SwarmStatus.Completed, result.SwarmResult.Status);
    }

    [Fact]
    public async Task TeamRunProfile_Sequential_ForcesMaxConcurrentToOne()
    {
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-seq-1",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "seq-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            RunMode = TeamRunMode.Sequential,
            MaxConcurrent = 7, // should be ignored by Sequential
            DecompositionMode = TeamDecompositionMode.Off,
        };
        registry.CreateTeam(team);

        var capturing = new CapturingSwarmOrchestrator();
        var decomposer = new StubDecomposer();
        var orchestrator = BuildOrchestrator(registry, capturing, decomposer);

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "sequential work",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        Assert.NotNull(capturing.LastConfig);
        Assert.Equal(1, capturing.LastConfig!.MaxConcurrent);
        // DecompositionMode.Off → decomposer must NOT be invoked.
        Assert.Equal(0, decomposer.CallCount);
    }

    [Fact]
    public async Task ExplicitRequestFlags_OverrideTeamProfile()
    {
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-ov-1",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "ov-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            RunMode = TeamRunMode.Sequential,
            MaxConcurrent = 1,
            FileLocksEnabled = false,
            QualityGateEnabled = false,
            DecompositionMode = TeamDecompositionMode.Off,
        };
        registry.CreateTeam(team);

        var capturing = new CapturingSwarmOrchestrator();
        var decomposer = new StubDecomposer();
        var orchestrator = BuildOrchestrator(registry, capturing, decomposer);

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "override everything",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
            MaxParallel = 5,
            LockFiles = true,
            QualityGate = true,
            Decompose = true,
        });

        Assert.NotNull(capturing.LastConfig);
        Assert.Equal(5, capturing.LastConfig!.MaxConcurrent);
        Assert.True(capturing.LastConfig.FileLocksEnabled);
        Assert.True(capturing.LastConfig.QualityGateEnabled);
        Assert.Equal(1, decomposer.CallCount);
    }

    [Fact]
    public async Task NoTeamId_FallsBackToGlobalSwarmConfigDefaults()
    {
        var registry = new FakeTeamRegistry();
        var capturing = new CapturingSwarmOrchestrator();
        var decomposer = new StubDecomposer();
        var orchestrator = BuildOrchestrator(registry, capturing, decomposer,
            new SwarmConfig
            {
                Enabled = true,
                MaxConcurrent = 4,
                FileLocksEnabled = false,
            });

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "engine-decided",
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        Assert.NotNull(capturing.LastConfig);
        // No team → engine decomposed into multiple tasks → global MaxConcurrent applies.
        Assert.Equal(4, capturing.LastConfig!.MaxConcurrent);
        Assert.False(capturing.LastConfig.FileLocksEnabled);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static AgentOrchestrator BuildOrchestrator(
        ITeamRegistry registry,
        ISwarmOrchestrator swarmOrchestrator,
        ISwarmDecomposer decomposer,
        SwarmConfig? swarmConfig = null)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new SovrantAgentFactory(services, NullLoggerFactory.Instance);
        var qualityGate = new SwarmQualityGate(factory, NullLogger<SwarmQualityGate>.Instance);
        return new AgentOrchestrator(
            swarmOrchestrator,
            decomposer,
            registry,
            new FakeAgentRunStore(),
            qualityGate,
            swarmConfig ?? new SwarmConfig { Enabled = true },
            NullLogger<AgentOrchestrator>.Instance);
    }

    private sealed class CapturingSwarmOrchestrator : ISwarmOrchestrator
    {
        public SwarmConfig? LastConfig { get; private set; }
        public SwarmPlan? LastPlan { get; private set; }

        public Task<SwarmResult> ExecuteAsync(
            SwarmPlan plan,
            SwarmConfig config,
            Action<SwarmEvent>? onEvent = null,
            SwarmExecutionContext? executionContext = null,
            CancellationToken ct = default)
        {
            LastPlan = plan;
            LastConfig = config;
            // Mark each task as completed so status resolves to Completed.
            foreach (var task in plan.Tasks)
            {
                task.Status = SwarmTaskStatus.Completed;
                task.Output = "ok";
            }
            return Task.FromResult(new SwarmResult
            {
                SwarmId = plan.Id,
                Status = SwarmStatus.Completed,
                Tasks = plan.Tasks,
                CombinedOutput = "ok",
                TotalTokensUsed = 0,
                Duration = TimeSpan.Zero,
            });
        }
    }

    private sealed class StubDecomposer : ISwarmDecomposer
    {
        public int CallCount { get; private set; }

        public Task<SwarmPlan> DecomposeAsync(string prompt, SwarmConfig config, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new SwarmPlan
            {
                Id = $"plan-{Guid.NewGuid():N}",
                OriginalPrompt = prompt,
                Tasks =
                [
                    new SwarmTaskNode { Id = "t1", Description = prompt, Wave = 0 },
                    new SwarmTaskNode { Id = "t2", Description = prompt + " (part 2)", Wave = 0 },
                ],
                WaveCount = 1,
            });
        }
    }

    private sealed class FakeAgentRunStore : IAgentRunStore
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

    /// <summary>Minimal in-memory <see cref="ITeamRegistry"/> with team-level support.</summary>
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
