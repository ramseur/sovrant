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
    public async Task TeamProfile_FileLocksDisabled_PropagatesToSwarmConfig()
    {
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-nolock-1",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "nolock-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            RunMode = TeamRunMode.Parallel,
            MaxConcurrent = 2,
            FileLocksEnabled = false,      // explicitly disabled on the team
            DecompositionMode = TeamDecompositionMode.Off,
        };
        registry.CreateTeam(team);

        var capturing = new CapturingSwarmOrchestrator();
        var orchestrator = BuildOrchestrator(registry, capturing, new StubDecomposer(),
            new SwarmConfig { Enabled = true, FileLocksEnabled = true }); // global default would say TRUE

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "parallel without locks",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        Assert.NotNull(capturing.LastConfig);
        // Team profile wins over global default.
        Assert.False(capturing.LastConfig!.FileLocksEnabled);
    }

    [Fact]
    public async Task RequestLockFiles_Override_BeatsTeamFileLocks()
    {
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-lockov-1",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "lockov-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            FileLocksEnabled = true,  // team says yes
        };
        registry.CreateTeam(team);

        var capturing = new CapturingSwarmOrchestrator();
        var orchestrator = BuildOrchestrator(registry, capturing, new StubDecomposer());

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "caller overrides",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
            LockFiles = false,        // caller says no
        });

        Assert.NotNull(capturing.LastConfig);
        Assert.False(capturing.LastConfig!.FileLocksEnabled);
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

    [Fact]
    public async Task QualityGate_Passes_WhenScoreMeetsThreshold_NoRetry()
    {
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-qg-pass",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "qg-pass",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            QualityGateEnabled = true,
            QualityGateThreshold = 7,
            DecompositionMode = TeamDecompositionMode.Off,
        };
        registry.CreateTeam(team);

        var capturing = new CapturingSwarmOrchestrator();
        var gate = new FakeSwarmQualityGate(new QualityVerdict(9, "pass", "good"));
        var orchestrator = BuildOrchestrator(registry, capturing, new StubDecomposer(), qualityGate: gate);

        var result = await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "gate-pass",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        Assert.Equal(1, capturing.ExecuteCallCount);
        Assert.Equal(1, gate.CallCount);
        Assert.NotNull(result.SwarmResult.QualityGate);
        Assert.Equal(9, result.SwarmResult.QualityGate!.Score);
    }

    [Fact]
    public async Task QualityGate_BelowThreshold_TriggersOneRetry_SecondVerdictWins()
    {
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-qg-retry",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "qg-retry",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            QualityGateEnabled = true,
            QualityGateThreshold = 7,
            DecompositionMode = TeamDecompositionMode.Off,
        };
        registry.CreateTeam(team);

        var capturing = new CapturingSwarmOrchestrator();
        var gate = new FakeSwarmQualityGate(
            new QualityVerdict(4, "needs_revision", "try again"),
            new QualityVerdict(9, "pass", "much better"));
        var orchestrator = BuildOrchestrator(registry, capturing, new StubDecomposer(), qualityGate: gate);

        var result = await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "gate-retry",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        // First execute + retry execute.
        Assert.Equal(2, capturing.ExecuteCallCount);
        Assert.Equal(2, gate.CallCount);
        Assert.NotNull(result.SwarmResult.QualityGate);
        // Second verdict is the authoritative one.
        Assert.Equal(9, result.SwarmResult.QualityGate!.Score);
    }

    [Fact]
    public async Task QualityGate_ThresholdComesFromTeamProfile()
    {
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-qg-threshold",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "qg-threshold",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            QualityGateEnabled = true,
            // Team demands a higher bar than the global default of 7.
            QualityGateThreshold = 9,
            DecompositionMode = TeamDecompositionMode.Off,
        };
        registry.CreateTeam(team);

        var capturing = new CapturingSwarmOrchestrator();
        // Score 8 passes global (>=7) but fails the team's stricter 9 bar.
        var gate = new FakeSwarmQualityGate(
            new QualityVerdict(8, "needs_revision", "close"),
            new QualityVerdict(10, "pass", "now perfect"));
        var orchestrator = BuildOrchestrator(registry, capturing, new StubDecomposer(), qualityGate: gate);

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "strict threshold",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        // Team profile threshold must have propagated, forcing a retry at score 8.
        Assert.Equal(2, capturing.ExecuteCallCount);
        Assert.Equal(9, capturing.LastConfig!.QualityGateThreshold);
    }

    [Fact]
    public async Task RoleAwareDecomposition_RewritesAgentTemplate_ToMatchedMemberName()
    {
        // Phase 78 Path 2 commit 6 — RoleAware decomposition must route
        // each decomposed task to the best-fit team member by overwriting
        // the decomposer's suggested template with the member's Name.
        // SwarmOrchestrator's team-member lookup keys on Name, so this
        // is what actually lands the work on the right person.
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-roleaware",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "role-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            DecompositionMode = TeamDecompositionMode.RoleAware,
        };
        registry.CreateTeam(team);

        registry.RegisterMember(new TeamMemberInfo
        {
            Id = "m-reviewer",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            Name = "senior-reviewer",
            Role = Sovrant.Agents.Models.AgentRole.Reviewer,
            Template = "reviewer",
            SystemPrompt = "review code",
            CreatedBy = "alice",
        });
        registry.RegisterMember(new TeamMemberInfo
        {
            Id = "m-coder",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            Name = "senior-coder",
            Role = Sovrant.Agents.Models.AgentRole.Coder,
            Template = "coder",
            SystemPrompt = "write code",
            CreatedBy = "alice",
        });

        var capturing = new CapturingSwarmOrchestrator();
        // Decomposer suggests generic template names; role-aware dispatch
        // must rewrite these to the matched member names.
        var decomposer = new TemplatingDecomposer(
            ("t1", "code-module", "coder"),
            ("t2", "review-module", "reviewer"));
        var orchestrator = BuildOrchestrator(registry, capturing, decomposer);

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "ship feature",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        Assert.NotNull(capturing.LastPlan);
        var byId = capturing.LastPlan!.Tasks.ToDictionary(t => t.Id);
        Assert.Equal("senior-coder", byId["t1"].AgentTemplate);
        Assert.Equal("senior-reviewer", byId["t2"].AgentTemplate);
    }

    [Fact]
    public async Task OpenDecomposition_DoesNotRewriteAgentTemplate()
    {
        // DecompositionMode.Open decomposes but does NOT constrain to
        // the team roster — the suggested template survives so ephemeral
        // or built-in workers can pick it up.
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-open",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "open-team",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            DecompositionMode = TeamDecompositionMode.Open,
        };
        registry.CreateTeam(team);

        registry.RegisterMember(new TeamMemberInfo
        {
            Id = "m-coder",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            Name = "senior-coder",
            Role = Sovrant.Agents.Models.AgentRole.Coder,
            Template = "coder",
            SystemPrompt = "write code",
            CreatedBy = "alice",
        });

        var capturing = new CapturingSwarmOrchestrator();
        var decomposer = new TemplatingDecomposer(("t1", "code-module", "coder"));
        var orchestrator = BuildOrchestrator(registry, capturing, decomposer);

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "ship feature",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        Assert.NotNull(capturing.LastPlan);
        // Original template name survives — no role-aware rewrite.
        Assert.Equal("coder", capturing.LastPlan!.Tasks.Single().AgentTemplate);
    }

    [Fact]
    public async Task RoleAwareDecomposition_WithEmptyRoster_LeavesAgentTemplateUntouched()
    {
        // If the team has no members yet, EnsembleSelector can't match
        // anyone. The run should still execute — the decomposer's suggested
        // template flows through untouched so ephemeral workers spawn.
        var registry = new FakeTeamRegistry();
        var team = new TeamInfo(
            Id: "team-empty-roster",
            WorkspaceId: "ws-1",
            ProjectId: null,
            Name: "empty-roster",
            Description: null,
            Origin: "user",
            CreatedBy: "alice",
            CreatedAt: DateTimeOffset.UtcNow)
        {
            DecompositionMode = TeamDecompositionMode.RoleAware,
        };
        registry.CreateTeam(team);
        // Intentionally no members registered.

        var capturing = new CapturingSwarmOrchestrator();
        var decomposer = new TemplatingDecomposer(("t1", "code-module", "coder"));
        var orchestrator = BuildOrchestrator(registry, capturing, decomposer);

        await orchestrator.RunAsync(new EnsembleRunRequest
        {
            Goal = "ship feature",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            UserId = "alice",
        });

        Assert.NotNull(capturing.LastPlan);
        Assert.Equal("coder", capturing.LastPlan!.Tasks.Single().AgentTemplate);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static AgentOrchestrator BuildOrchestrator(
        ITeamRegistry registry,
        ISwarmOrchestrator swarmOrchestrator,
        ISwarmDecomposer decomposer,
        SwarmConfig? swarmConfig = null,
        ISwarmQualityGate? qualityGate = null)
    {
        var gate = qualityGate ?? new FakeSwarmQualityGate(new QualityVerdict(10, "pass", "ok"));
        return new AgentOrchestrator(
            swarmOrchestrator,
            decomposer,
            registry,
            new FakeAgentRunStore(),
            gate,
            swarmConfig ?? new SwarmConfig { Enabled = true },
            NullLogger<AgentOrchestrator>.Instance);
    }

    private sealed class FakeSwarmQualityGate : ISwarmQualityGate
    {
        private readonly Queue<QualityVerdict> _verdicts;
        private readonly QualityVerdict _fallback;

        public FakeSwarmQualityGate(params QualityVerdict[] verdicts)
        {
            _verdicts = new Queue<QualityVerdict>(verdicts);
            _fallback = verdicts.Length > 0 ? verdicts[^1] : new QualityVerdict(10, "pass", string.Empty);
        }

        public int CallCount { get; private set; }

        public Task<QualityVerdict> ReviewAsync(string swarmId, string originalPrompt, string combinedOutput, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_verdicts.Count > 0 ? _verdicts.Dequeue() : _fallback);
        }
    }

    private sealed class CapturingSwarmOrchestrator : ISwarmOrchestrator
    {
        public SwarmConfig? LastConfig { get; private set; }
        public SwarmPlan? LastPlan { get; private set; }
        public int ExecuteCallCount { get; private set; }

        public Task<SwarmResult> ExecuteAsync(
            SwarmPlan plan,
            SwarmConfig config,
            Action<SwarmEvent>? onEvent = null,
            SwarmExecutionContext? executionContext = null,
            CancellationToken ct = default)
        {
            LastPlan = plan;
            LastConfig = config;
            ExecuteCallCount++;
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

    /// <summary>Decomposer that emits tasks with caller-specified templates, so role-aware dispatch can be observed.</summary>
    private sealed class TemplatingDecomposer : ISwarmDecomposer
    {
        private readonly (string Id, string Description, string Template)[] _tasks;

        public TemplatingDecomposer(params (string Id, string Description, string Template)[] tasks)
        {
            _tasks = tasks;
        }

        public Task<SwarmPlan> DecomposeAsync(string prompt, SwarmConfig config, CancellationToken ct = default) =>
            Task.FromResult(new SwarmPlan
            {
                Id = $"plan-{Guid.NewGuid():N}",
                OriginalPrompt = prompt,
                Tasks = _tasks
                    .Select(t => new SwarmTaskNode
                    {
                        Id = t.Id,
                        Description = t.Description,
                        AgentTemplate = t.Template,
                        Wave = 0,
                    })
                    .ToList<SwarmTaskNode>(),
                WaveCount = 1,
            });
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
