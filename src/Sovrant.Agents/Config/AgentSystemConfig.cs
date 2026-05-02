using Sovrant.Runtime.Workspaces;

namespace Sovrant.Agents.Config;

/// <summary>
/// Configuration for the orchestration system.
/// <para>
/// The simplest way to switch backends is the <c>AGENT_MODE</c> environment variable:
/// <list type="bullet">
///   <item><c>AGENT_MODE=isolated</c> (or unset) — uses <see cref="Isolated.ProcessBasedOrchestrationSystem"/> (default)</item>
///   <item><c>AGENT_MODE=shared</c> — uses <see cref="Shared.InProcessOrchestrationSystem"/></item>
/// </list>
/// Concurrency / timeout tuning (Bucket-B) reads env &gt; <see cref="IWorkspaceSettingsStore"/>
/// &gt; defaults via <see cref="Resolve"/>; <c>AGENT_MODE</c> stays env-only because the
/// process-isolation backend is fixed at startup.
/// </para>
/// </summary>
public sealed class AgentSystemConfig
{
    /// <summary>
    /// When <see langword="true"/> (default), uses <see cref="Isolated.ProcessBasedOrchestrationSystem"/>
    /// (process-per-agent, stdin/stdout) for full process-level isolation. When
    /// <see langword="false"/>, uses <see cref="Shared.InProcessOrchestrationSystem"/>
    /// (in-process async channels, shared memory).
    /// </summary>
    public bool UseIsolatedAgents { get; init; } = true;

    /// <summary>
    /// Maximum number of agents that can execute tasks simultaneously in a single
    /// orchestration run. Default: 5. Env override: <c>SOVRANT_AGENT_MAX_CONCURRENT</c>.
    /// </summary>
    public int MaxConcurrentAgents { get; init; } = 5;

    /// <summary>
    /// Timeout for a single agent task in seconds. The task is cancelled and an error
    /// result is returned if this limit is exceeded. Default: 120 seconds.
    /// Env override: <c>SOVRANT_AGENT_TASK_TIMEOUT_SECONDS</c>.
    /// </summary>
    public int TaskTimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Builds an <see cref="AgentSystemConfig"/> from the <c>AGENT_MODE</c> environment variable.
    /// Tuning fields keep their defaults; use <see cref="Resolve"/> to layer in
    /// workspace_settings / env overrides.
    /// </summary>
    public static AgentSystemConfig FromEnvironment()
    {
        var mode = Environment.GetEnvironmentVariable("AGENT_MODE");
        return new AgentSystemConfig
        {
            UseIsolatedAgents = !string.Equals(mode, "shared", StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Builds an <see cref="AgentSystemConfig"/> with <c>AGENT_MODE</c> selecting the
    /// backend, and concurrency / timeout fields resolved from env &gt; the persistent
    /// settings store &gt; defaults.
    /// </summary>
    public static AgentSystemConfig Resolve(IWorkspaceSettingsStore? settings)
    {
        var mode = Environment.GetEnvironmentVariable("AGENT_MODE");
        return new AgentSystemConfig
        {
            UseIsolatedAgents = !string.Equals(mode, "shared", StringComparison.OrdinalIgnoreCase),
            MaxConcurrentAgents = WorkspaceSettingsResolver.ResolveInt(
                settings, WorkspaceSettingsKeys.AgentMaxConcurrent,
                "SOVRANT_AGENT_MAX_CONCURRENT", fallback: 5),
            TaskTimeoutSeconds = WorkspaceSettingsResolver.ResolveInt(
                settings, WorkspaceSettingsKeys.AgentTaskTimeoutSeconds,
                "SOVRANT_AGENT_TASK_TIMEOUT_SECONDS", fallback: 120),
        };
    }
}
