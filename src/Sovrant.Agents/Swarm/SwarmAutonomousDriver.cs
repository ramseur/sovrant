using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Missions;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Phase 67 — autonomous driver that advances a mission by decomposing its
/// goal into a <see cref="SwarmPlan"/> and running it through
/// <see cref="ISwarmOrchestrator"/>. Swarm events are projected onto the
/// mission journal so the mission's history is consistent regardless of
/// which driver advanced it.
/// </summary>
public sealed partial class SwarmAutonomousDriver : IAutonomousDriver
{
    public const string DriverName = "swarm";

    [LoggerMessage(Level = LogLevel.Information, Message = "SwarmDriver: advancing mission {MissionId}")]
    private static partial void LogStart(ILogger logger, string missionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SwarmDriver: mission {MissionId} terminal state {State} (swarm {SwarmStatus})")]
    private static partial void LogTerminal(ILogger logger, string missionId, MissionStatus state, SwarmStatus swarmStatus);

    private readonly IMissionStore _store;
    private readonly ISwarmDecomposer _decomposer;
    private readonly ISwarmOrchestrator _orchestrator;
    private readonly SwarmConfig _config;
    private readonly ILogger<SwarmAutonomousDriver> _logger;

    public SwarmAutonomousDriver(
        IMissionStore store,
        ISwarmDecomposer decomposer,
        ISwarmOrchestrator orchestrator,
        SwarmConfig config,
        ILogger<SwarmAutonomousDriver>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _decomposer = decomposer ?? throw new ArgumentNullException(nameof(decomposer));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SwarmAutonomousDriver>.Instance;
    }

    public string Name => DriverName;

    public DriverCapabilities Capabilities { get; } = new(
        SupportsReplanning: false,
        SupportsParallelSteps: true,
        SupportsHumanAcceptance: false,
        MaxStepsPerCycle: int.MaxValue);

    public async Task<Mission> AdvanceAsync(string missionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(missionId);

        var mission = await _store.GetAsync(missionId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"mission {missionId} not found");

        if (mission.Status is MissionStatus.Completed
                          or MissionStatus.Failed
                          or MissionStatus.Cancelled)
        {
            return mission;
        }

        LogStart(_logger, missionId);

        var plan = await _decomposer.DecomposeAsync(mission.Goal, _config, ct).ConfigureAwait(false);
        var planJson = JsonSerializer.Serialize(new
        {
            driver = DriverName,
            swarm_id = plan.Id,
            task_count = plan.Tasks.Count,
            wave_count = plan.WaveCount,
        });

        await _store.AppendEventAsync(missionId, MissionEventTypes.PlanRevised, planJson,
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
        await _store.UpdateStateAsync(missionId, MissionStatus.Running, planJson: planJson, ct: ct).ConfigureAwait(false);
        await _store.AppendEventAsync(missionId, MissionEventTypes.RunStarted,
            JsonSerializer.Serialize(new { driver = DriverName, swarm_id = plan.Id }),
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);

        var buffered = new ConcurrentQueue<SwarmEvent>();

        SwarmResult result;
        try
        {
            result = await _orchestrator.ExecuteAsync(
                plan, _config,
                onEvent: buffered.Enqueue,
                executionContext: null,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await FlushBufferedEventsAsync(missionId, mission, buffered, ct: CancellationToken.None).ConfigureAwait(false);
            await _store.AppendEventAsync(missionId, MissionEventTypes.Cancelled, "{}",
                mission.WorkspaceId, mission.ProjectId, CancellationToken.None).ConfigureAwait(false);
            await _store.UpdateStateAsync(missionId, MissionStatus.Cancelled,
                completedAt: DateTimeOffset.UtcNow, ct: CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await FlushBufferedEventsAsync(missionId, mission, buffered, ct).ConfigureAwait(false);
            await _store.AppendEventAsync(missionId, MissionEventTypes.Failed,
                JsonSerializer.Serialize(new { error = ex.Message }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.UpdateStateAsync(missionId, MissionStatus.Failed,
                completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);
            return await _store.GetAsync(missionId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"mission {missionId} disappeared after failure");
        }

        await FlushBufferedEventsAsync(missionId, mission, buffered, ct).ConfigureAwait(false);

        var terminalStatus = result.Status switch
        {
            SwarmStatus.Completed => MissionStatus.Completed,
            SwarmStatus.Cancelled => MissionStatus.Cancelled,
            _ => MissionStatus.Failed,
        };
        var terminalEventType = terminalStatus switch
        {
            MissionStatus.Completed => MissionEventTypes.Completed,
            MissionStatus.Cancelled => MissionEventTypes.Cancelled,
            _ => MissionEventTypes.Failed,
        };

        var runCompletedPayload = JsonSerializer.Serialize(new
        {
            driver = DriverName,
            swarm_id = result.SwarmId,
            swarm_status = result.Status.ToString(),
            total_tokens = result.TotalTokensUsed,
            duration_s = result.Duration.TotalSeconds,
            combined_output_len = result.CombinedOutput?.Length ?? 0,
        });

        await _store.AppendEventAsync(missionId, MissionEventTypes.RunCompleted, runCompletedPayload,
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
        await _store.AppendEventAsync(missionId, terminalEventType, "{}",
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
        await _store.UpdateStateAsync(missionId, terminalStatus,
            completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);

        LogTerminal(_logger, missionId, terminalStatus, result.Status);

        return await _store.GetAsync(missionId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"mission {missionId} disappeared after completion");
    }

    private async Task FlushBufferedEventsAsync(
        string missionId, Mission mission, ConcurrentQueue<SwarmEvent> buffered, CancellationToken ct)
    {
        while (buffered.TryDequeue(out var ev))
        {
            var payload = JsonSerializer.Serialize<object>(ev);
            await _store.AppendEventAsync(missionId, SwarmEventTypeFor(ev), payload,
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
        }
    }

    private static string SwarmEventTypeFor(SwarmEvent ev) => ev switch
    {
        SwarmEvent.TaskStarted => "swarm_task_started",
        SwarmEvent.TaskCompleted => "swarm_task_completed",
        SwarmEvent.TaskFailed => "swarm_task_failed",
        SwarmEvent.FileConflict => "swarm_file_conflict",
        SwarmEvent.BudgetExceeded => "swarm_budget_exceeded",
        SwarmEvent.QualityGateStarted => "swarm_quality_started",
        SwarmEvent.QualityGateCompleted => "swarm_quality_completed",
        SwarmEvent.WaveCompleted => "swarm_wave_completed",
        SwarmEvent.PlanCreated => "swarm_plan_created",
        SwarmEvent.SwarmCompleted => "swarm_completed",
        SwarmEvent.CoordinationReceived => "swarm_coordination_in",
        SwarmEvent.CoordinationSent => "swarm_coordination_out",
        _ => "swarm_event",
    };
}
