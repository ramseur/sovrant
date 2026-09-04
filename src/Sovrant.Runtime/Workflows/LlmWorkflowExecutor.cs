using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 51 — default <see cref="IWorkflowExecutor"/>. Drives one mission
/// one engine run forward and records every state transition in the
/// <c>mission_events</c> journal so the mission's history is fully
/// reconstructable from the journal alone. Delegates the actual step
/// execution to <see cref="IExecutor"/> (Phase 51 step C) — this class
/// is the glue between the mission layer and the engine layer, not a
/// new execution strategy.
/// </summary>
public sealed partial class LlmWorkflowExecutor : IWorkflowExecutor
{
    [LoggerMessage(Level = LogLevel.Information, Message = "WorkflowExecutor: running workflow {WorkflowId} (status {Status})")]
    private static partial void LogRunStarted(ILogger logger, string workflowId, WorkflowStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "WorkflowExecutor: workflow {WorkflowId} terminal state {State} ({Reason})")]
    private static partial void LogTerminal(ILogger logger, string workflowId, WorkflowStatus state, string reason);

    private readonly IWorkflowStore _store;
    private readonly IWorkflowPlanner _planner;
    private readonly IExecutor _engineExecutor;
    private readonly IAcceptanceGate _gate;
    private readonly ILogger<LlmWorkflowExecutor> _logger;

    public LlmWorkflowExecutor(
        IWorkflowStore store,
        IWorkflowPlanner planner,
        IExecutor engineExecutor,
        IAcceptanceGate gate,
        ILogger<LlmWorkflowExecutor>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _engineExecutor = engineExecutor ?? throw new ArgumentNullException(nameof(engineExecutor));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LlmWorkflowExecutor>.Instance;
    }

    public async Task<Workflow> RunAsync(string workflowId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowId);

        var mission = await _store.GetAsync(workflowId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"mission {workflowId} not found");

        // Terminal missions are no-ops — caller may race two RunAsyncs and
        // we want the second one to be a cheap idempotent return, not an
        // error or a rerun.
        if (mission.Status is WorkflowStatus.Completed
                          or WorkflowStatus.Failed
                          or WorkflowStatus.Cancelled)
        {
            return mission;
        }

        LogRunStarted(_logger, mission.Id, mission.Status);

        // ── Plan ─────────────────────────────────────────────────────────
        var plan = await _planner.PlanAsync(mission, ct).ConfigureAwait(false);
        var planJson = JsonSerializer.Serialize(new
        {
            plan_id = plan.Id,
            plan_version = plan.PlanVersion,
            goal = plan.Goal,
            steps = plan.Steps.Select(s => new
            {
                index = s.Index,
                intent = s.Intent,
                expected = s.ExpectedOutcome,
                tier = s.ModelTier.ToString(),
            }),
        });

        await _store.UpdateStateAsync(
            mission.Id, WorkflowStatus.Running, planJson: planJson, ct: ct).ConfigureAwait(false);
        await _store.AppendEventAsync(
            mission.Id,
            WorkflowEventTypes.PlanRevised,
            JsonSerializer.Serialize(new { plan_id = plan.Id, plan_version = plan.PlanVersion }),
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);

        // ── Execute ──────────────────────────────────────────────────────
        var runId = $"run-{Guid.NewGuid():N}";
        var runCtx = new EngineRunContext(
            RuntimeRunId: runId,
            SessionId: mission.SessionId ?? mission.Id,
            WorkspaceId: mission.WorkspaceId,
            ProjectId: mission.ProjectId);

        await _store.AppendEventAsync(
            mission.Id,
            WorkflowEventTypes.RunStarted,
            JsonSerializer.Serialize(new { runtime_run_id = runId, plan_id = plan.Id }),
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);

        // Workflow layer never provides a re-plan callback here — within a
        // single RunAsync, a re-plan is represented as a fresh mission
        // cycle (next RunAsync call). The engine executor's internal
        // re-plans still work through the Replanner delegate if the
        // planner supports it; for the simple planner we hand back the
        // same plan so the executor terminates cleanly.
        ExecutionResult result;
        try
        {
            result = await _engineExecutor.ExecuteAsync(
                plan,
                runCtx,
                replanner: (currentPlan, _, _, _) => Task.FromResult(currentPlan),
                ct).ConfigureAwait(false);
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
            LogTerminal(_logger, mission.Id, WorkflowStatus.Failed, ex.Message);
            return (await _store.GetAsync(mission.Id, ct).ConfigureAwait(false))!;
        }

        await _store.AppendEventAsync(
            mission.Id,
            WorkflowEventTypes.RunCompleted,
            JsonSerializer.Serialize(new
            {
                runtime_run_id = runId,
                terminal_state = result.TerminalState.ToString(),
                replan_count = result.ReplanCount,
                step_count = result.Outcomes.Count,
            }),
            mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);

        // ── Acceptance gate ──────────────────────────────────────────────
        var decision = await _gate.EvaluateAsync(mission, result, ct).ConfigureAwait(false);

        if (decision.RequiresHuman)
        {
            await _store.AppendEventAsync(
                mission.Id,
                WorkflowEventTypes.Paused,
                JsonSerializer.Serialize(new { reason = decision.Reason }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.UpdateStateAsync(
                mission.Id, WorkflowStatus.AwaitingHuman, ct: ct).ConfigureAwait(false);
            LogTerminal(_logger, mission.Id, WorkflowStatus.AwaitingHuman, decision.Reason);
        }
        else if (decision.Accepted)
        {
            await _store.AppendEventAsync(
                mission.Id,
                WorkflowEventTypes.AcceptanceApproved,
                JsonSerializer.Serialize(new { reason = decision.Reason }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.AppendEventAsync(
                mission.Id,
                WorkflowEventTypes.Completed,
                "{}",
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.UpdateStateAsync(
                mission.Id, WorkflowStatus.Completed,
                completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);
            LogTerminal(_logger, mission.Id, WorkflowStatus.Completed, decision.Reason);
        }
        else
        {
            await _store.AppendEventAsync(
                mission.Id,
                WorkflowEventTypes.AcceptanceRejected,
                JsonSerializer.Serialize(new { reason = decision.Reason }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.AppendEventAsync(
                mission.Id,
                WorkflowEventTypes.Failed,
                JsonSerializer.Serialize(new { reason = decision.Reason }),
                mission.WorkspaceId, mission.ProjectId, ct).ConfigureAwait(false);
            await _store.UpdateStateAsync(
                mission.Id, WorkflowStatus.Failed,
                completedAt: DateTimeOffset.UtcNow, ct: ct).ConfigureAwait(false);
            LogTerminal(_logger, mission.Id, WorkflowStatus.Failed, decision.Reason);
        }

        return (await _store.GetAsync(mission.Id, ct).ConfigureAwait(false))!;
    }
}
