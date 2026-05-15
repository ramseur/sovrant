using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 step I — production <see cref="IStepRunner"/>. Runs one
/// <see cref="RuntimeStep"/> by asking a session-scoped
/// <see cref="IConversationRuntime"/> to execute a single turn with a
/// prompt derived from the step's intent, expected outcome, and
/// (optionally compacted) prior-outcome history.
///
/// This runner intentionally stays on top of the existing agentic loop:
/// the conversation runtime already owns tool dispatch, permission
/// checks, and streaming, so the step runner's job is just to frame
/// the turn, collect the events, and convert them into a
/// <see cref="StepOutcome"/> the executor can act on.
///
/// Per-step model-tier routing (<see cref="RuntimeStep.ModelTier"/>) is
/// left as a future enhancement — the runner records the requested tier
/// in the step's observation JSON so the trace reflects the planner's
/// intent even when the underlying router does not yet pin per tier.
/// </summary>
public sealed partial class LlmStepRunner : IStepRunner
{
    [LoggerMessage(Level = LogLevel.Information, Message = "LlmStepRunner: step {Step} attempt {Attempt} for run {RunId} using session {Session}")]
    private static partial void LogStepStarted(ILogger logger, int step, int attempt, string runId, string session);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LlmStepRunner: step {Step} in run {RunId} saw tool error from {Tool}: {Error}")]
    private static partial void LogToolError(ILogger logger, int step, string runId, string tool, string error);

    private readonly IRuntimeSessionPool _sessionPool;
    private readonly IContextCompactor _compactor;
    private readonly ILogger<LlmStepRunner> _logger;
    private readonly TimeProvider _clock;
    private readonly StepRunnerOptions _options;

    public LlmStepRunner(
        IRuntimeSessionPool sessionPool,
        IContextCompactor compactor,
        StepRunnerOptions? options = null,
        ILogger<LlmStepRunner>? logger = null,
        TimeProvider? clock = null)
    {
        _sessionPool = sessionPool ?? throw new ArgumentNullException(nameof(sessionPool));
        _compactor = compactor ?? throw new ArgumentNullException(nameof(compactor));
        _options = options ?? StepRunnerOptions.Default;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LlmStepRunner>.Instance;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<StepOutcome> RunAsync(
        RuntimeStep step,
        StepExecutionContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = _clock.GetUtcNow();
        LogStepStarted(_logger, step.Index, context.AttemptNumber, context.RuntimeRunId, context.SessionId);

        var pooled = await _sessionPool
            .GetOrCreateAsync(context.SessionId, scopedRouterOverride: null, ownerUserId: null, ct)
            .ConfigureAwait(false);

        // Compact prior history if it exceeds the runner budget.
        var compacted = await _compactor
            .CompactAsync(context.PriorOutcomes, _options.PriorOutcomeBudget, ct)
            .ConfigureAwait(false);

        var prompt = BuildPrompt(step, compacted);

        // Serialise turns through the per-session lock — the pool documents
        // this is required whenever we call RunTurnAsync.
        await pooled.Lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var (finalText, toolResults, sawToolError, stopReason, errorMessage) =
                await CollectTurnAsync(pooled.Runtime, prompt, ct).ConfigureAwait(false);

            var completedAt = _clock.GetUtcNow();

            // Decide terminal status for this step:
            // - Any hard RuntimeError from the runtime → Failed.
            // - A tool result with IsError=true → treat as Contradicted
            //   (the planner asked for one outcome, we saw an error).
            // - Otherwise → Succeeded.
            if (errorMessage is not null)
            {
                return new StepOutcome(
                    StepIndex: step.Index,
                    Status: StepStatus.Failed,
                    Summary: Truncate(finalText.Length > 0 ? finalText : errorMessage, 256),
                    ObservationJson: BuildObservationJson(step, toolResults, stopReason),
                    MatchedExpectation: false,
                    StartedAt: startedAt,
                    CompletedAt: completedAt,
                    ErrorMessage: errorMessage);
            }

            if (sawToolError)
            {
                return new StepOutcome(
                    StepIndex: step.Index,
                    Status: StepStatus.Contradicted,
                    Summary: Truncate(finalText.Length > 0 ? finalText : "tool reported error", 256),
                    ObservationJson: BuildObservationJson(step, toolResults, stopReason),
                    MatchedExpectation: false,
                    StartedAt: startedAt,
                    CompletedAt: completedAt);
            }

            return new StepOutcome(
                StepIndex: step.Index,
                Status: StepStatus.Succeeded,
                Summary: Truncate(finalText.Length > 0 ? finalText : "(no text)", 256),
                ObservationJson: BuildObservationJson(step, toolResults, stopReason),
                MatchedExpectation: true,
                StartedAt: startedAt,
                CompletedAt: completedAt);
        }
        finally
        {
            pooled.Lock.Release();
        }
    }

    private async Task<(string FinalText, List<ToolCall> Tools, bool SawError, string? StopReason, string? ErrorMessage)>
        CollectTurnAsync(IConversationRuntime runtime, string prompt, CancellationToken ct)
    {
        var text = new StringBuilder();
        var tools = new List<ToolCall>();
        var sawError = false;
        string? stopReason = null;
        string? errorMessage = null;

        await foreach (var evt in runtime.RunTurnAsync(prompt, ct).ConfigureAwait(false))
        {
            switch (evt)
            {
                case RuntimeEvent.TextChunk tc:
                    text.Append(tc.Text);
                    break;

                case RuntimeEvent.ToolResult tr:
                    tools.Add(new ToolCall(tr.ToolName, tr.IsError, Truncate(tr.Content, 512)));
                    if (tr.IsError)
                    {
                        sawError = true;
                        LogToolError(_logger, 0, runtime.SessionId, tr.ToolName, Truncate(tr.Content, 160));
                    }
                    break;

                case RuntimeEvent.TurnComplete tc:
                    stopReason = tc.StopReason;
                    break;

                case RuntimeEvent.RuntimeError err:
                    errorMessage = err.Message;
                    break;

                case RuntimeEvent.PermissionDenied pd:
                    errorMessage = $"Permission denied for tool '{pd.ToolName}': {pd.Reason}";
                    break;
            }
        }

        return (text.ToString(), tools, sawError, stopReason, errorMessage);
    }

    private static string BuildPrompt(RuntimeStep step, CompactedContext compacted)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Planned step {step.Index}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Intent: {step.Intent}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Expected outcome: {step.ExpectedOutcome}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Model tier requested: {step.ModelTier}");
        if (step.AllowedTools is { Count: > 0 })
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"Allowed tools: {string.Join(", ", step.AllowedTools)}");
        }

        if (!string.IsNullOrEmpty(compacted.Summary))
        {
            sb.AppendLine();
            sb.AppendLine("### Compacted earlier history");
            sb.AppendLine(compacted.Summary);
        }

        if (compacted.PreservedOutcomes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Recent outcomes");
            foreach (var o in compacted.PreservedOutcomes)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"- step {o.StepIndex} [{o.Status}]: {Truncate(o.Summary ?? "(no summary)", 120)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Execute the planned step now. Use tools if you need them.");
        return sb.ToString();
    }

    private static string BuildObservationJson(
        RuntimeStep step, List<ToolCall> tools, string? stopReason)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            model_tier = step.ModelTier.ToString(),
            stop_reason = stopReason,
            tool_calls = tools,
        });
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }

    private sealed record ToolCall(string Name, bool IsError, string Content);
}

/// <summary>
/// Tunable knobs for <see cref="LlmStepRunner"/>. Kept as a record so
/// tests and composition roots can override individual fields without
/// touching the runner's constructor signature.
/// </summary>
/// <param name="PriorOutcomeBudget">Character budget passed to the compactor when rendering prior-outcome history into the step prompt.</param>
public sealed record StepRunnerOptions(int PriorOutcomeBudget = 4_000)
{
    public static StepRunnerOptions Default { get; } = new();
}
