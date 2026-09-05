using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 51 — enhanced <see cref="IWorkflowExecutor"/> that fans out
/// independent plan steps across concurrent engine runs. Sits on top
/// of the existing <see cref="LlmWorkflowExecutor"/> lifecycle (plan →
/// execute → gate → journal) but replaces the single-threaded execute
/// phase with parallel execution when the plan has multiple steps.
///
/// Each concurrent engine run gets its own runtime run id and writes
/// to the scratchpad so results are visible to the next plan wave.
/// The overall mission result merges all concurrent outcomes and
/// passes them through the same acceptance gate.
///
/// Falls back to sequential execution (via the underlying
/// <see cref="IExecutor"/>) if the plan has only one step or if
/// parallel execution is disabled.
/// </summary>
public sealed partial class ParallelWorkflowExecutor : IWorkflowExecutor
{
    [LoggerMessage(Level = LogLevel.Information, Message = "ParallelWorkflowExecutor: fanning out {Count} steps for workflow {WorkflowId}")]
    private static partial void LogFanOut(ILogger logger, int count, string workflowId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ParallelWorkflowExecutor: parallel step {Step} failed for workflow {WorkflowId}: {Error}")]
    private static partial void LogStepFailed(ILogger logger, int step, string workflowId, string error);

    private readonly IWorkflowStore _store;
    private readonly IWorkflowPlanner _planner;
    private readonly IStepRunner _stepRunner;
    private readonly IRuntimeTraceStore _traceStore;
    private readonly IAcceptanceGate _gate;
    private readonly IWorkflowScratchpadStore _scratchpad;
    private readonly WorkflowSessionNotifier _sessionNotifier;
    private readonly ILogger<ParallelWorkflowExecutor> _logger;

    /// <summary>
    /// Maximum number of concurrent step executions. Prevents resource
    /// exhaustion when the planner produces many steps.
    /// </summary>
    public int MaxConcurrency { get; init; } = 4;

    public ParallelWorkflowExecutor(
        IWorkflowStore store,
        IWorkflowPlanner planner,
        IStepRunner stepRunner,
        IRuntimeTraceStore traceStore,
        IAcceptanceGate gate,
        IWorkflowScratchpadStore scratchpad,
        WorkflowSessionNotifier sessionNotifier,
        ILogger<ParallelWorkflowExecutor>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _stepRunner = stepRunner ?? throw new ArgumentNullException(nameof(stepRunner));
        _traceStore = traceStore ?? throw new ArgumentNullException(nameof(traceStore));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _scratchpad = scratchpad ?? throw new ArgumentNullException(nameof(scratchpad));
        _sessionNotifier = sessionNotifier ?? throw new ArgumentNullException(nameof(sessionNotifier));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ParallelWorkflowExecutor>.Instance;
    }

    public async Task<Workflow> RunAsync(string workflowId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowId);

        var mission = await _store.GetAsync(workflowId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"mission {workflowId} not found");

        if (mission.Status is WorkflowStatus.Completed
                          or WorkflowStatus.Failed
                          or WorkflowStatus.Cancelled)
        {
            return mission;
        }

        // ── Plan ─────────────────────────────────────────────────────────
        var plan = await _planner.PlanAsync(mission, ct).ConfigureAwait(false);
        var planJson = JsonSerializer.Serialize(new
        {
            plan_id = plan.Id,
            plan_version = plan.PlanVersion,
            goal = plan.Goal,
            step_count = plan.Steps.Count,
        });

        await _store.UpdateStateAsync(
            mission.Id, WorkflowStatus.Running, planJson: planJson, ct: ct).ConfigureAwait(false);
        await _store.AppendEventAsync(
            mission.Id,
            WorkflowEventTypes.PlanRevised,
            JsonSerializer.Serialize(new { plan_id = plan.Id, plan_version = plan.PlanVersion }),
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);

        // ── Execute — fan out if multiple steps ──────────────────────────
        var runId = $"run-{Guid.NewGuid():N}";
        await _store.AppendEventAsync(
            mission.Id,
            WorkflowEventTypes.RunStarted,
            JsonSerializer.Serialize(new
            {
                runtime_run_id = runId,
                plan_id = plan.Id,
                parallel = plan.Steps.Count > 1,
            }),
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);

        IReadOnlyList<StepOutcome> outcomes;
        try
        {
            if (plan.Steps.Count > 1)
            {
                LogFanOut(_logger, plan.Steps.Count, mission.Id);
                outcomes = await ExecuteParallelAsync(mission, plan, runId, ct).ConfigureAwait(false);
            }
            else
            {
                outcomes = await ExecuteSequentialAsync(mission, plan, runId, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _store.AppendEventAsync(
                mission.Id,
                WorkflowEventTypes.Failed,
                JsonSerializer.Serialize(new { error = ex.Message }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.UpdateStateAsync(
                mission.Id, WorkflowStatus.Failed,
                completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);
            var crashedMission = (await _store.GetAsync(mission.Id, ct).ConfigureAwait(false))!;
            await _sessionNotifier.NotifyAsync(crashedMission, ct).ConfigureAwait(false);
            return crashedMission;
        }

        // Build a synthetic ExecutionResult for the gate.
        var terminalState = outcomes.All(o => o.Status == StepStatus.Succeeded)
            ? ExecutionTerminalState.Completed
            : ExecutionTerminalState.FailedAfterReplans;
        var result = new ExecutionResult(plan, outcomes, 0, terminalState);

        await _store.AppendEventAsync(
            mission.Id,
            WorkflowEventTypes.RunCompleted,
            JsonSerializer.Serialize(new
            {
                runtime_run_id = runId,
                terminal_state = result.TerminalState.ToString(),
                step_count = outcomes.Count,
                parallel = plan.Steps.Count > 1,
            }),
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);

        // ── Acceptance gate ──────────────────────────────────────────────
        var decision = await _gate.EvaluateAsync(mission, result, ct).ConfigureAwait(false);

        if (decision.RequiresHuman)
        {
            await _store.AppendEventAsync(
                mission.Id, WorkflowEventTypes.Paused,
                JsonSerializer.Serialize(new { reason = decision.Reason }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.UpdateStateAsync(
                mission.Id, WorkflowStatus.AwaitingHuman, ct: ct).ConfigureAwait(false);
        }
        else if (decision.Accepted)
        {
            await _store.AppendEventAsync(
                mission.Id, WorkflowEventTypes.AcceptanceApproved,
                JsonSerializer.Serialize(new { reason = decision.Reason }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.AppendEventAsync(
                mission.Id, WorkflowEventTypes.Completed, "{}",
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.UpdateStateAsync(
                mission.Id, WorkflowStatus.Completed,
                completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);
        }
        else
        {
            await _store.AppendEventAsync(
                mission.Id, WorkflowEventTypes.AcceptanceRejected,
                JsonSerializer.Serialize(new { reason = decision.Reason }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.AppendEventAsync(
                mission.Id, WorkflowEventTypes.Failed,
                JsonSerializer.Serialize(new { reason = decision.Reason }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.UpdateStateAsync(
                mission.Id, WorkflowStatus.Failed,
                completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);
        }

        var finalMission = (await _store.GetAsync(mission.Id, ct).ConfigureAwait(false))!;
        await _sessionNotifier.NotifyAsync(finalMission, ct).ConfigureAwait(false);
        return finalMission;
    }

    private async Task<IReadOnlyList<StepOutcome>> ExecuteParallelAsync(
        Workflow mission, RuntimePlan plan, string runId, CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = new Task<StepOutcome>[plan.Steps.Count];

        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
#pragma warning disable CA2025 // Task.WhenAll in the try block ensures all tasks complete before Dispose
            tasks[i] = RunStepWithSemaphoreAsync(semaphore, step, mission, plan, runId, ct);
#pragma warning restore CA2025
        }

        StepOutcome[] outcomes;
        try
        {
            outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Dispose();
        }

        // Write outcomes to scratchpad so the next plan wave can read them.
        foreach (var outcome in outcomes)
        {
            await _scratchpad.AppendAsync(new WorkflowScratchpadEntry(
                WorkflowId: mission.Id,
                StepIndex: outcome.StepIndex,
                Namespace: "step_outcome",
                Key: $"step_{outcome.StepIndex}",
                Value: JsonSerializer.Serialize(new
                {
                    status = outcome.Status.ToString(),
                    summary = outcome.Summary,
                    matched = outcome.MatchedExpectation,
                }),
                WorkspaceId: mission.WorkspaceId,
                ProjectId: mission.ProjectId), ct).ConfigureAwait(false);
        }

        return outcomes;
    }

    private async Task<StepOutcome> RunStepWithSemaphoreAsync(
        SemaphoreSlim semaphore,
        RuntimeStep step,
        Workflow mission,
        RuntimePlan plan,
        string runId,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var ctx = new StepExecutionContext(
                RuntimeRunId: $"{runId}-step{step.Index}",
                PlanId: plan.Id,
                PlanVersion: plan.PlanVersion,
                SessionId: mission.SessionId ?? mission.Id,
                WorkspaceId: mission.WorkspaceId,
                ProjectId: mission.ProjectId,
                AttemptNumber: 1,
                PriorOutcomes: Array.Empty<StepOutcome>());

            return await _stepRunner.RunAsync(step, ctx, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogStepFailed(_logger, step.Index, mission.Id, ex.Message);
            return new StepOutcome(
                StepIndex: step.Index,
                Status: StepStatus.Failed,
                Summary: ex.Message,
                ObservationJson: null,
                MatchedExpectation: false,
                StartedAt: DateTimeOffset.UtcNow,
                CompletedAt: DateTimeOffset.UtcNow,
                ErrorMessage: ex.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<IReadOnlyList<StepOutcome>> ExecuteSequentialAsync(
        Workflow mission, RuntimePlan plan, string runId, CancellationToken ct)
    {
        var outcomes = new List<StepOutcome>();
        foreach (var step in plan.Steps)
        {
            var ctx = new StepExecutionContext(
                RuntimeRunId: runId,
                PlanId: plan.Id,
                PlanVersion: plan.PlanVersion,
                SessionId: mission.SessionId ?? mission.Id,
                WorkspaceId: mission.WorkspaceId,
                ProjectId: mission.ProjectId,
                AttemptNumber: 1,
                PriorOutcomes: outcomes);

            var outcome = await _stepRunner.RunAsync(step, ctx, ct).ConfigureAwait(false);
            outcomes.Add(outcome);
        }
        return outcomes;
    }
}
