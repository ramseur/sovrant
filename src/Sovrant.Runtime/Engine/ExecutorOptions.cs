using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — tunable limits for <see cref="LlmExecutor"/>. Kept separate
/// from the executor class so tests can construct an executor with tight
/// bounds (e.g. 1 re-plan, 1 retry) without having to mock a config object.
/// </summary>
/// <param name="MaxReplans">Hard ceiling on re-plan requests per run. Default 3. A run that exhausts its budget without recovering terminates with <see cref="ExecutionTerminalState.FailedAfterReplans"/>.</param>
/// <param name="MaxStepRetries">Number of retries allowed on a <see cref="StepStatus.Failed"/> step before the executor escalates to a re-plan. Default 2 — i.e. the third consecutive failure triggers a re-plan with <see cref="ReplanReason.RepeatedFailure"/>.</param>
public sealed record ExecutorOptions(
    int MaxReplans = 3,
    int MaxStepRetries = 2)
{
    /// <summary>Default options — 3 re-plans, 2 retries per step.</summary>
    public static ExecutorOptions Default { get; } = new();

    /// <summary>
    /// Resolves <see cref="ExecutorOptions"/> from env &gt; <see cref="IWorkspaceSettingsStore"/>
    /// &gt; defaults. Env vars: <c>SOVRANT_EXECUTOR_MAX_REPLANS</c>,
    /// <c>SOVRANT_EXECUTOR_MAX_STEP_RETRIES</c>.
    /// </summary>
    public static ExecutorOptions Resolve(IWorkspaceSettingsStore? settings) => new(
        MaxReplans: WorkspaceSettingsResolver.ResolveInt(
            settings, WorkspaceSettingsKeys.ExecutorMaxReplans,
            "SOVRANT_EXECUTOR_MAX_REPLANS", fallback: 3),
        MaxStepRetries: WorkspaceSettingsResolver.ResolveInt(
            settings, WorkspaceSettingsKeys.ExecutorMaxStepRetries,
            "SOVRANT_EXECUTOR_MAX_STEP_RETRIES", fallback: 2));
}

/// <summary>
/// Ambient context a caller hands to <see cref="LlmExecutor.ExecuteAsync"/>.
/// The executor copies these fields onto every <see cref="StepExecutionContext"/>
/// and every trace entry so a recovered run can be attributed to the right
/// session/workspace/project without re-reading the plan.
/// </summary>
/// <param name="RuntimeRunId">Opaque id for one engine run. The mission layer mints one per mission step; the bare CLI mints a new one per prompt.</param>
/// <param name="SessionId">Owning session id.</param>
/// <param name="WorkspaceId">Owning workspace id, if any.</param>
/// <param name="ProjectId">Owning project id, if any.</param>
/// <param name="EventSink">Phase 59 — optional callback that receives <see cref="Conversation.RuntimeEvent"/>s during execution (plan presented, step progress, etc.).</param>
public sealed record EngineRunContext(
    string RuntimeRunId,
    string SessionId,
    string? WorkspaceId = null,
    string? ProjectId = null,
    Action<Conversation.RuntimeEvent>? EventSink = null);
