using Microsoft.Extensions.Logging;
using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Orchestration;

/// <summary>
/// Phase 52 — unified agent orchestration engine. Merges the public surface
/// of <see cref="SwarmOrchestrator"/> and the delegation path of
/// <c>TeamDelegateTool</c> into one engine with opt-in feature flags.
///
/// Three creation modes:
/// <list type="number">
///   <item>One pre-existing team: members come from <see cref="ITeamRegistry"/></item>
///   <item>Multiple teams: <see cref="EnsembleSelector"/> routes tasks to best-fit team members</item>
///   <item>Engine-decided: <see cref="ISwarmDecomposer"/> builds a plan, ephemeral workers from templates</item>
/// </list>
///
/// The heavy machinery — DAG decomposition, file locking, wave scheduling,
/// quality gate — is opt-in via request flags. A simple delegation runs
/// through the same engine with all flags off.
/// </summary>
public sealed partial class AgentOrchestrator : IAgentOrchestrator
{
    [LoggerMessage(Level = LogLevel.Information, Message = "AgentOrchestrator: starting run {RunId} mode={Mode} for goal '{Goal}'")]
    private static partial void LogRunStart(ILogger logger, string runId, string mode, string goal);

    [LoggerMessage(Level = LogLevel.Information, Message = "AgentOrchestrator: run {RunId} completed with status {Status}")]
    private static partial void LogRunComplete(ILogger logger, string runId, SwarmStatus status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AgentOrchestrator: run {RunId} failed: {Error}")]
    private static partial void LogRunFailed(ILogger logger, string runId, string error);

    private readonly ISwarmOrchestrator _swarmOrchestrator;
    private readonly ISwarmDecomposer _decomposer;
    private readonly ITeamRegistry _teamRegistry;
    private readonly IAgentRunStore _runStore;
    private readonly SwarmQualityGate _qualityGate;
    private readonly SwarmConfig _swarmConfig;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        ISwarmOrchestrator swarmOrchestrator,
        ISwarmDecomposer decomposer,
        ITeamRegistry teamRegistry,
        IAgentRunStore runStore,
        SwarmQualityGate qualityGate,
        SwarmConfig swarmConfig,
        ILogger<AgentOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(swarmOrchestrator);
        ArgumentNullException.ThrowIfNull(decomposer);
        ArgumentNullException.ThrowIfNull(teamRegistry);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(qualityGate);
        ArgumentNullException.ThrowIfNull(swarmConfig);
        ArgumentNullException.ThrowIfNull(logger);
        _swarmOrchestrator = swarmOrchestrator;
        _decomposer = decomposer;
        _teamRegistry = teamRegistry;
        _runStore = runStore;
        _qualityGate = qualityGate;
        _swarmConfig = swarmConfig;
        _logger = logger;
    }

    public async Task<EnsembleRunResult> RunAsync(EnsembleRunRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runId = $"run-{Guid.NewGuid():N}";
        var mode = ResolveMode(request);
        var teamId = ResolveTeamId(request);

        LogRunStart(_logger, runId, mode, request.Goal);

        // ── Record run in the unified ledger ────────────────────────────
        await _runStore.CreateAsync(new AgentRunRecord(
            RunId: runId,
            ParentRunId: request.ParentRunId,
            TeamId: teamId,
            MemberId: null,
            WorkspaceId: request.WorkspaceId,
            ProjectId: request.ProjectId,
            UserId: request.UserId,
            Kind: mode switch
            {
                "delegation" => "delegation",
                "team" or "multi-team" => "delegation",
                _ => "swarm-task",
            },
            Status: "running",
            StartedAt: DateTimeOffset.UtcNow), ct).ConfigureAwait(false);

        try
        {
            // ── Resolve plan ────────────────────────────────────────────
            var shouldDecompose = request.Decompose ?? (request.Plan is null);
            SwarmPlan plan;

            if (request.Plan is not null)
            {
                plan = new SwarmPlan
                {
                    Id = runId,
                    OriginalPrompt = request.Goal,
                    Tasks = request.Plan,
                    WaveCount = (request.Plan.Count > 0 ? request.Plan.Max(t => t.Wave) + 1 : 1),
                    TeamId = teamId,
                };
            }
            else if (shouldDecompose)
            {
                plan = await _decomposer.DecomposeAsync(request.Goal, _swarmConfig, ct).ConfigureAwait(false);
                plan.TeamId = teamId;
            }
            else
            {
                // Single-task plan wrapping the goal (delegation mode)
                plan = new SwarmPlan
                {
                    Id = runId,
                    OriginalPrompt = request.Goal,
                    Tasks = [new SwarmTaskNode
                    {
                        Id = "task-01",
                        Description = request.Goal,
                        Wave = 0,
                    }],
                    WaveCount = 1,
                    TeamId = teamId,
                };
            }

            // ── Build effective config ──────────────────────────────────
            var isSingleTask = plan.Tasks.Count <= 1;
            var effectiveConfig = new SwarmConfig
            {
                Enabled = true,
                MaxConcurrent = request.MaxParallel ?? (isSingleTask ? 1 : _swarmConfig.MaxConcurrent),
                MaxTokenBudget = _swarmConfig.MaxTokenBudget,
                MaxRetries = _swarmConfig.MaxRetries,
                QualityGateEnabled = request.QualityGate ?? !isSingleTask,
                TaskTimeoutSeconds = _swarmConfig.TaskTimeoutSeconds,
                Permissions = request.Permissions,
            };

            var swarmContext = new SwarmExecutionContext(
                UserId: request.UserId,
                WorkspaceId: request.WorkspaceId,
                ProjectId: request.ProjectId);

            // ── Execute via SwarmOrchestrator ────────────────────────────
            var result = await _swarmOrchestrator.ExecuteAsync(
                plan, effectiveConfig, request.OnEvent, swarmContext, ct).ConfigureAwait(false);

            // ── Update run ledger ───────────────────────────────────────
            var statusStr = result.Status switch
            {
                SwarmStatus.Completed => "succeeded",
                SwarmStatus.Failed => "failed",
                SwarmStatus.Cancelled => "cancelled",
                _ => "succeeded",
            };
            await _runStore.UpdateStatusAsync(
                runId, statusStr, result.TotalTokensUsed, 0, null, ct).ConfigureAwait(false);

            LogRunComplete(_logger, runId, result.Status);

            // ── Publish team if requested ───────────────────────────────
            string? publishedTeamId = null;
            if (request.PublishTeamOnComplete && result.Status == SwarmStatus.Completed)
            {
                publishedTeamId = PublishEphemeralTeam(request, plan, runId);
            }

            return new EnsembleRunResult
            {
                RunId = runId,
                SwarmResult = result,
                TeamId = teamId,
                PublishedTeamId = publishedTeamId,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRunFailed(_logger, runId, ex.Message);
            await _runStore.UpdateStatusAsync(runId, "failed", ct: ct).ConfigureAwait(false);
            throw;
        }
    }

    private static string ResolveMode(EnsembleRunRequest request)
    {
        if (request.TeamIds is { Count: > 1 }) return "multi-team";
        if (request.TeamId is not null || request.TeamIds is { Count: 1 }) return "team";
        if (request.Plan is not null) return "delegation";
        return "swarm";
    }

    private static string? ResolveTeamId(EnsembleRunRequest request)
    {
        if (request.TeamId is not null) return request.TeamId;
        if (request.TeamIds is { Count: > 0 }) return request.TeamIds[0];
        return null;
    }

    private string? PublishEphemeralTeam(EnsembleRunRequest request, SwarmPlan plan, string runId)
    {
        var teamId = $"team-{Guid.NewGuid():N}";
        var team = new TeamInfo(
            Id: teamId,
            WorkspaceId: request.WorkspaceId,
            ProjectId: request.ProjectId,
            Name: $"swarm-{runId[..12]}",
            Description: $"Auto-published from swarm run: {request.Goal}",
            Origin: "swarm-published",
            CreatedBy: request.UserId,
            CreatedAt: DateTimeOffset.UtcNow);

        _teamRegistry.CreateTeam(team);

        foreach (var task in plan.Tasks)
        {
            var member = new TeamMemberInfo
            {
                Id = $"member-{Guid.NewGuid():N}",
                TeamId = teamId,
                WorkspaceId = request.WorkspaceId,
                ProjectId = request.ProjectId,
                Name = task.AgentTemplate ?? $"worker-{task.Id}",
                Role = Models.AgentRole.Coder,
                Template = task.AgentTemplate,
                SystemPrompt = task.Description,
                CreatedBy = request.UserId,
            };
            _teamRegistry.RegisterMember(member);
        }

        return teamId;
    }
}
