using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — default <see cref="IExecutor"/> implementation. Owns the
/// re-plan loop, the retry policy, and the durable trace log. Delegates
/// the actual per-step work to an injected <see cref="IStepRunner"/> so
/// tests can drive the loop with a fake runner and the production step
/// runner (which talks to <c>SmartRouter</c> and the tool registry) can
/// land in Phase 51 step I without touching the loop semantics.
///
/// Crash-safety rule: every state transition is written to
/// <see cref="IRuntimeTraceStore"/> <i>before</i> its side effect. The
/// ordering guarantees:
///   1. <c>plan_created</c>            — before the first step runs.
///   2. <c>step_started</c>            — before <see cref="IStepRunner.RunAsync"/> is called.
///   3. <c>step_completed|step_failed</c> — after the step runner returns.
///   4. <c>replan_requested</c>        — before calling the replanner callback.
///   5. <c>run_completed</c>           — last write, after the terminal state is known.
///
/// A process crash between (2) and (3) leaves a <c>step_started</c> with
/// no matching completion row, which is exactly what
/// <see cref="IRuntimeTraceStore.ListInFlightRunsAsync"/> looks for at
/// recovery time.
/// </summary>
public sealed partial class LlmExecutor : IExecutor
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Engine run {RunId}: re-plan budget exhausted after {Count} re-plans")]
    private static partial void LogReplanBudgetExhausted(ILogger logger, string runId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Engine run {RunId}: replanner returned plan version {NewVersion} <= current {CurrentVersion}; using it anyway")]
    private static partial void LogReplanVersionRegression(ILogger logger, string runId, int newVersion, int currentVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine run {RunId}: step {Step} failed on attempt {Attempt}/{Max}, retrying")]
    private static partial void LogStepRetry(ILogger logger, string runId, int step, int attempt, int max);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine run {RunId}: plan presented to user (requires_approval={RequiresApproval})")]
    private static partial void LogPlanPresented(ILogger logger, string runId, bool requiresApproval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Engine run {RunId}: step {Step}/{Total} progress — {Intent}")]
    private static partial void LogStepProgress(ILogger logger, string runId, int step, int total, string intent);

    private readonly IStepRunner _stepRunner;
    private readonly IRuntimeTraceStore _traceStore;
    private readonly ILiveSettings<ExecutorOptions> _options;
    private readonly ILogger<LlmExecutor> _logger;
    private readonly TimeProvider _clock;
    private readonly Governance.IPlanPresenter? _planPresenter;
    private readonly Governance.PlanApprovalGate? _approvalGate;
    private readonly Governance.PlanProgressTracker? _progressTracker;

    /// <summary>
    /// DI ctor — accepts the hot-reloadable <see cref="ILiveSettings{ExecutorOptions}"/>.
    /// Tests with a static <see cref="ExecutorOptions"/> wrap it in
    /// <see cref="LiveSettings.Static{T}"/>.
    /// </summary>
    public LlmExecutor(
        IStepRunner stepRunner,
        IRuntimeTraceStore traceStore,
        ILiveSettings<ExecutorOptions> options,
        ILogger<LlmExecutor>? logger = null,
        TimeProvider? clock = null,
        Governance.IPlanPresenter? planPresenter = null,
        Governance.PlanApprovalGate? approvalGate = null,
        Governance.PlanProgressTracker? progressTracker = null)
    {
        _stepRunner = stepRunner ?? throw new ArgumentNullException(nameof(stepRunner));
        _traceStore = traceStore ?? throw new ArgumentNullException(nameof(traceStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LlmExecutor>.Instance;
        _clock = clock ?? TimeProvider.System;
        _planPresenter = planPresenter;
        _approvalGate = approvalGate;
        _progressTracker = progressTracker;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        RuntimePlan plan,
        EngineRunContext runContext,
        Replanner replanner,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(runContext);
        ArgumentNullException.ThrowIfNull(replanner);

        var currentPlan = plan;
        var outcomes = new List<StepOutcome>();
        var replanCount = 0;

        await AppendPlanCreatedAsync(currentPlan, runContext, ct).ConfigureAwait(false);

        // Phase 59b — present plan to user and check if approval is needed.
        EmitPlanPresented(currentPlan, runContext);

        try
        {
            // Outer loop: one iteration per (current) plan version. A re-plan
            // swaps currentPlan and restarts at step index 0 of the new plan.
            // The planner is responsible for skipping already-completed work
            // by omitting those steps from its returned patch.
            while (true)
            {
                var terminal = await RunPlanAsync(
                    currentPlan, runContext, outcomes, ct).ConfigureAwait(false);

                switch (terminal.Kind)
                {
                    case InnerTerminalKind.Completed:
                        await AppendRunCompletedAsync(
                            currentPlan, runContext, ExecutionTerminalState.Completed, ct)
                            .ConfigureAwait(false);
                        return new ExecutionResult(
                            currentPlan, outcomes, replanCount, ExecutionTerminalState.Completed);

                    case InnerTerminalKind.ReplanRequested:
                        if (replanCount >= _options.Current.MaxReplans)
                        {
                            LogReplanBudgetExhausted(_logger, runContext.RuntimeRunId, replanCount);
                            await AppendRunCompletedAsync(
                                currentPlan, runContext, ExecutionTerminalState.FailedAfterReplans, ct)
                                .ConfigureAwait(false);
                            return new ExecutionResult(
                                currentPlan, outcomes, replanCount, ExecutionTerminalState.FailedAfterReplans);
                        }

                        await AppendReplanRequestedAsync(
                            currentPlan, runContext, terminal.Trigger!, ct).ConfigureAwait(false);
                        replanCount++;
                        var patched = await replanner(
                            currentPlan, outcomes, terminal.Trigger!, ct).ConfigureAwait(false);
                        // The patched plan should carry the same family id
                        // with an incremented version. We tolerate either
                        // shape but log if the planner returned something
                        // with a lower version.
                        if (patched.PlanVersion <= currentPlan.PlanVersion)
                        {
                            LogReplanVersionRegression(
                                _logger, runContext.RuntimeRunId, patched.PlanVersion, currentPlan.PlanVersion);
                        }
                        currentPlan = patched;
                        await AppendPlanCreatedAsync(currentPlan, runContext, ct).ConfigureAwait(false);
                        continue;
                }
            }
        }
        catch (OperationCanceledException)
        {
            await AppendRunCompletedAsync(
                currentPlan, runContext, ExecutionTerminalState.Cancelled, CancellationToken.None)
                .ConfigureAwait(false);
            return new ExecutionResult(
                currentPlan, outcomes, replanCount, ExecutionTerminalState.Cancelled);
        }
    }

    // ── Plan execution ───────────────────────────────────────────────────

    /// <summary>
    /// Runs every step in the current plan version, in order. Returns an
    /// inner terminal describing whether the plan finished cleanly or the
    /// executor wants a re-plan (which is handled by the caller's outer
    /// loop). Retries on <see cref="StepStatus.Failed"/> up to the
    /// configured budget before escalating to a re-plan request.
    /// </summary>
    private async Task<InnerTerminal> RunPlanAsync(
        RuntimePlan plan,
        EngineRunContext runContext,
        List<StepOutcome> outcomes,
        CancellationToken ct)
    {
        foreach (var step in plan.Steps)
        {
            ct.ThrowIfCancellationRequested();

            var attempt = 1;
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                await AppendStepStartedAsync(plan, step, runContext, attempt, ct).ConfigureAwait(false);

                // Phase 59e — emit step progress (started).
                EmitStepProgress(plan, step, runContext, "started");

                var stepCtx = new StepExecutionContext(
                    RuntimeRunId: runContext.RuntimeRunId,
                    PlanId: plan.Id,
                    PlanVersion: plan.PlanVersion,
                    SessionId: runContext.SessionId,
                    WorkspaceId: runContext.WorkspaceId,
                    ProjectId: runContext.ProjectId,
                    AttemptNumber: attempt,
                    PriorOutcomes: outcomes.AsReadOnly());

                StepOutcome outcome;
                try
                {
                    outcome = await _stepRunner.RunAsync(step, stepCtx, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation bubbles up to the top-level handler.
                    throw;
                }
#pragma warning disable CA1031 // step runner is user code — any throw must be converted to a Failed outcome so the executor can retry or re-plan
                catch (Exception ex)
                {
                    outcome = new StepOutcome(
                        StepIndex: step.Index,
                        Status: StepStatus.Failed,
                        Summary: $"Step runner threw: {ex.GetType().Name}",
                        ObservationJson: null,
                        MatchedExpectation: false,
                        StartedAt: _clock.GetUtcNow(),
                        CompletedAt: _clock.GetUtcNow(),
                        ErrorMessage: ex.Message);
                }
#pragma warning restore CA1031

                outcomes.Add(outcome);
                await AppendStepCompletedAsync(plan, step, runContext, outcome, ct).ConfigureAwait(false);

                // Phase 59e — emit step progress (completed/failed).
                EmitStepProgress(plan, step, runContext,
                    outcome.Status == StepStatus.Failed ? "failed" : "completed");

                switch (outcome.Status)
                {
                    case StepStatus.Succeeded:
                    case StepStatus.Skipped:
                        // Move to next step.
                        goto nextStep;

                    case StepStatus.Contradicted:
                        // Cheap re-plan trigger: the step ran but the result
                        // disagreed with the planner's expectation. Always
                        // escalates to a re-plan (no retry, since retrying
                        // the same step would produce the same result).
                        return InnerTerminal.Replan(new ReplanTrigger(
                            Kind: ReplanReason.Contradiction,
                            Detail: outcome.Summary,
                            OffendingStepIndex: step.Index));

                    case StepStatus.Failed:
                        var maxRetries = _options.Current.MaxStepRetries;
                        if (attempt <= maxRetries)
                        {
                            LogStepRetry(
                                _logger, runContext.RuntimeRunId, step.Index, attempt, maxRetries + 1);
                            attempt++;
                            continue;
                        }
                        // Retries exhausted → escalate to a re-plan.
                        return InnerTerminal.Replan(new ReplanTrigger(
                            Kind: ReplanReason.RepeatedFailure,
                            Detail: outcome.ErrorMessage ?? outcome.Summary,
                            OffendingStepIndex: step.Index));
                }
            }

            nextStep:;
        }

        return InnerTerminal.Completed();
    }

    // ── Trace writes ─────────────────────────────────────────────────────

    private Task AppendPlanCreatedAsync(RuntimePlan plan, EngineRunContext runContext, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            plan_id = plan.Id,
            plan_version = plan.PlanVersion,
            goal = plan.Goal,
            step_count = plan.Steps.Count,
        });
        return _traceStore.AppendAsync(new RuntimeTraceEntry(
            RuntimeRunId: runContext.RuntimeRunId,
            PlanId: plan.Id,
            PlanVersion: plan.PlanVersion,
            StepIndex: null,
            EntryType: "plan_created",
            Payload: payload,
            Timestamp: _clock.GetUtcNow(),
            SessionId: runContext.SessionId,
            WorkspaceId: runContext.WorkspaceId,
            ProjectId: runContext.ProjectId), ct);
    }

    private Task AppendStepStartedAsync(
        RuntimePlan plan, RuntimeStep step, EngineRunContext runContext, int attempt, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            intent = step.Intent,
            expected_outcome = step.ExpectedOutcome,
            model_tier = step.ModelTier.ToString(),
            attempt,
        });
        return _traceStore.AppendAsync(new RuntimeTraceEntry(
            RuntimeRunId: runContext.RuntimeRunId,
            PlanId: plan.Id,
            PlanVersion: plan.PlanVersion,
            StepIndex: step.Index,
            EntryType: "step_started",
            Payload: payload,
            Timestamp: _clock.GetUtcNow(),
            SessionId: runContext.SessionId,
            WorkspaceId: runContext.WorkspaceId,
            ProjectId: runContext.ProjectId), ct);
    }

    private Task AppendStepCompletedAsync(
        RuntimePlan plan, RuntimeStep step, EngineRunContext runContext, StepOutcome outcome, CancellationToken ct)
    {
        var entryType = outcome.Status == StepStatus.Failed ? "step_failed" : "step_completed";
        var payload = JsonSerializer.Serialize(new
        {
            status = outcome.Status.ToString(),
            summary = outcome.Summary,
            matched_expectation = outcome.MatchedExpectation,
            observation = outcome.ObservationJson,
            error = outcome.ErrorMessage,
        });
        return _traceStore.AppendAsync(new RuntimeTraceEntry(
            RuntimeRunId: runContext.RuntimeRunId,
            PlanId: plan.Id,
            PlanVersion: plan.PlanVersion,
            StepIndex: step.Index,
            EntryType: entryType,
            Payload: payload,
            Timestamp: _clock.GetUtcNow(),
            SessionId: runContext.SessionId,
            WorkspaceId: runContext.WorkspaceId,
            ProjectId: runContext.ProjectId), ct);
    }

    private Task AppendReplanRequestedAsync(
        RuntimePlan plan, EngineRunContext runContext, ReplanTrigger trigger, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            reason = trigger.Kind.ToString(),
            detail = trigger.Detail,
            offending_step_index = trigger.OffendingStepIndex,
        });
        return _traceStore.AppendAsync(new RuntimeTraceEntry(
            RuntimeRunId: runContext.RuntimeRunId,
            PlanId: plan.Id,
            PlanVersion: plan.PlanVersion,
            StepIndex: trigger.OffendingStepIndex,
            EntryType: "replan_requested",
            Payload: payload,
            Timestamp: _clock.GetUtcNow(),
            SessionId: runContext.SessionId,
            WorkspaceId: runContext.WorkspaceId,
            ProjectId: runContext.ProjectId), ct);
    }

    private Task AppendRunCompletedAsync(
        RuntimePlan plan, EngineRunContext runContext, ExecutionTerminalState terminal, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            terminal_state = terminal.ToString(),
        });
        return _traceStore.AppendAsync(new RuntimeTraceEntry(
            RuntimeRunId: runContext.RuntimeRunId,
            PlanId: plan.Id,
            PlanVersion: plan.PlanVersion,
            StepIndex: null,
            EntryType: "run_completed",
            Payload: payload,
            Timestamp: _clock.GetUtcNow(),
            SessionId: runContext.SessionId,
            WorkspaceId: runContext.WorkspaceId,
            ProjectId: runContext.ProjectId), ct);
    }

    // ── Phase 59 event emission ─────────────────────────────────────────

    /// <summary>Phase 59b — formats and emits a PlanPresented event via the run context's event sink.</summary>
    private void EmitPlanPresented(RuntimePlan plan, EngineRunContext runContext)
    {
        if (runContext.EventSink is null)
            return;

        var formatted = _planPresenter?.FormatPlan(plan) ?? $"Plan: {plan.Goal} ({plan.Steps.Count} steps)";
        var requiresApproval = _approvalGate is not null &&
            Governance.PlanApprovalGate.RequiresApproval(
                plan, Governance.PlanApprovalMode.ApproveDestructive);

        LogPlanPresented(_logger, runContext.RuntimeRunId, requiresApproval);
        runContext.EventSink(new Conversation.RuntimeEvent.PlanPresented(
            plan.Id, formatted, requiresApproval));
    }

    /// <summary>Phase 59e — emits a StepProgress event via the run context's event sink.</summary>
    private void EmitStepProgress(RuntimePlan plan, RuntimeStep step, EngineRunContext runContext, string status)
    {
        if (runContext.EventSink is null)
            return;

        LogStepProgress(_logger, runContext.RuntimeRunId, step.Index + 1, plan.Steps.Count, step.Intent);

        var progressEvent = _progressTracker is not null
            ? Governance.PlanProgressTracker.CreateEvent(plan, step, status)
            : new Conversation.RuntimeEvent.StepProgress(
                step.Index + 1, plan.Steps.Count, step.Intent, status);

        runContext.EventSink(progressEvent);
    }

    // ── Inner terminal ───────────────────────────────────────────────────

    private readonly record struct InnerTerminal(InnerTerminalKind Kind, ReplanTrigger? Trigger)
    {
        public static InnerTerminal Completed() => new(InnerTerminalKind.Completed, null);
        public static InnerTerminal Replan(ReplanTrigger trigger) => new(InnerTerminalKind.ReplanRequested, trigger);
    }

    private enum InnerTerminalKind
    {
        Completed,
        ReplanRequested,
    }
}
