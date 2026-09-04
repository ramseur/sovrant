using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Phase 67 — autonomous driver that advances a workflow by decomposing its
/// goal into a <see cref="SwarmPlan"/> and running it through
/// <see cref="ISwarmOrchestrator"/>. Swarm events are projected onto the
/// workflow journal so the workflow's history is consistent regardless of
/// which driver advanced it.
/// </summary>
public sealed partial class SwarmAutonomousDriver : IAutonomousDriver
{
    public const string DriverName = "swarm";

    [LoggerMessage(Level = LogLevel.Information, Message = "SwarmDriver: advancing workflow {WorkflowId}")]
    private static partial void LogStart(ILogger logger, string workflowId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SwarmDriver: workflow {WorkflowId} terminal state {State} (swarm {SwarmStatus})")]
    private static partial void LogTerminal(ILogger logger, string workflowId, WorkflowStatus state, SwarmStatus swarmStatus);

    private readonly IWorkflowStore _store;
    private readonly ISwarmDecomposer _decomposer;
    private readonly ISwarmOrchestrator _orchestrator;
    private readonly SwarmConfig _config;
    private readonly ILogger<SwarmAutonomousDriver> _logger;

    public SwarmAutonomousDriver(
        IWorkflowStore store,
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

    public async Task<Workflow> AdvanceAsync(string workflowId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);

        var workflow = await _store.GetAsync(workflowId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"workflow {workflowId} not found");

        if (workflow.Status is WorkflowStatus.Completed
                          or WorkflowStatus.Failed
                          or WorkflowStatus.Cancelled)
        {
            return workflow;
        }

        LogStart(_logger, workflowId);

        var plan = await _decomposer.DecomposeAsync(workflow.Goal, _config, ct).ConfigureAwait(false);
        var planJson = JsonSerializer.Serialize(new
        {
            driver = DriverName,
            swarm_id = plan.Id,
            task_count = plan.Tasks.Count,
            wave_count = plan.WaveCount,
        });

        await _store.AppendEventAsync(workflowId, WorkflowEventTypes.PlanRevised, planJson,
            workflow.WorkspaceId, workflow.ProjectId, ct).ConfigureAwait(false);
        await _store.UpdateStateAsync(workflowId, WorkflowStatus.Running, planJson: planJson, ct: ct).ConfigureAwait(false);
        await _store.AppendEventAsync(workflowId, WorkflowEventTypes.RunStarted,
            JsonSerializer.Serialize(new { driver = DriverName, swarm_id = plan.Id }),
            workflow.WorkspaceId, workflow.ProjectId, ct).ConfigureAwait(false);

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
            await FlushBufferedEventsAsync(workflowId, workflow, buffered, ct: CancellationToken.None).ConfigureAwait(false);
            await _store.AppendEventAsync(workflowId, WorkflowEventTypes.Cancelled, "{}",
                workflow.WorkspaceId, workflow.ProjectId, CancellationToken.None).ConfigureAwait(false);
            await _store.UpdateStateAsync(workflowId, WorkflowStatus.Cancelled,
                completedAt: DateTimeOffset.UtcNow, ct: CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await FlushBufferedEventsAsync(workflowId, workflow, buffered, ct).ConfigureAwait(false);
            await _store.AppendEventAsync(workflowId, WorkflowEventTypes.Failed,
                JsonSerializer.Serialize(new { error = ex.Message }),
                workflow.WorkspaceId, workflow.ProjectId, ct).ConfigureAwait(false);
            await _store.UpdateStateAsync(workflowId, WorkflowStatus.Failed,
                completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);
            return await _store.GetAsync(workflowId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"workflow {workflowId} disappeared after failure");
        }

        await FlushBufferedEventsAsync(workflowId, workflow, buffered, ct).ConfigureAwait(false);

        var terminalStatus = result.Status switch
        {
            SwarmStatus.Completed => WorkflowStatus.Completed,
            SwarmStatus.Cancelled => WorkflowStatus.Cancelled,
            _ => WorkflowStatus.Failed,
        };
        var terminalEventType = terminalStatus switch
        {
            WorkflowStatus.Completed => WorkflowEventTypes.Completed,
            WorkflowStatus.Cancelled => WorkflowEventTypes.Cancelled,
            _ => WorkflowEventTypes.Failed,
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

        await _store.AppendEventAsync(workflowId, WorkflowEventTypes.RunCompleted, runCompletedPayload,
            workflow.WorkspaceId, workflow.ProjectId, ct).ConfigureAwait(false);
        await _store.AppendEventAsync(workflowId, terminalEventType, "{}",
            workflow.WorkspaceId, workflow.ProjectId, ct).ConfigureAwait(false);
        await _store.UpdateStateAsync(workflowId, terminalStatus,
            completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);

        LogTerminal(_logger, workflowId, terminalStatus, result.Status);

        return await _store.GetAsync(workflowId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"workflow {workflowId} disappeared after completion");
    }

    private async Task FlushBufferedEventsAsync(
        string workflowId, Workflow workflow, ConcurrentQueue<SwarmEvent> buffered, CancellationToken ct)
    {
        while (buffered.TryDequeue(out var ev))
        {
            var payload = JsonSerializer.Serialize<object>(ev);
            await _store.AppendEventAsync(workflowId, SwarmEventTypeFor(ev), payload,
                workflow.WorkspaceId, workflow.ProjectId, ct).ConfigureAwait(false);
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
