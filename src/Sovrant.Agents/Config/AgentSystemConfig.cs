namespace Sovrant.Agents.Config;

/// <summary>
/// Configuration for the multi-agent system.
/// <para>
/// The simplest way to switch backends is the <c>AGENT_MODE</c> environment variable:
/// <list type="bullet">
///   <item><c>AGENT_MODE=isolated</c> (or unset) — uses <see cref="Isolated.ProcessBasedMultiAgentSystem"/> (default)</item>
///   <item><c>AGENT_MODE=shared</c> — uses <see cref="Shared.InProcessMultiAgentSystem"/></item>
/// </list>
/// Alternatively, set <see cref="UseIsolatedAgents"/> programmatically before calling
/// <c>services.AddMultiAgentSystem(config)</c>.
/// </para>
/// </summary>
public sealed class AgentSystemConfig
{
    /// <summary>
    /// When <see langword="true"/> (default), uses <see cref="Isolated.ProcessBasedMultiAgentSystem"/>
    /// (process-per-agent, stdin/stdout) for full process-level isolation. When
    /// <see langword="false"/>, uses <see cref="Shared.InProcessMultiAgentSystem"/>
    /// (in-process async channels, shared memory).
    /// </summary>
    public bool UseIsolatedAgents { get; init; } = true;

    /// <summary>
    /// Maximum number of agents that can execute tasks simultaneously in a single
    /// multi-agent run. Default: 5.
    /// </summary>
    public int MaxConcurrentAgents { get; init; } = 5;

    /// <summary>
    /// Timeout for a single agent task in seconds. The task is cancelled and an error
    /// result is returned if this limit is exceeded. Default: 120 seconds.
    /// </summary>
    public int TaskTimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Builds an <see cref="AgentSystemConfig"/> from the <c>AGENT_MODE</c> environment variable.
    /// <c>AGENT_MODE=shared</c> sets <see cref="UseIsolatedAgents"/> = <see langword="false"/>;
    /// any other value (including absent) keeps the isolated default.
    /// </summary>
    public static AgentSystemConfig FromEnvironment()
    {
        var mode = Environment.GetEnvironmentVariable("AGENT_MODE");
        return new AgentSystemConfig
        {
            UseIsolatedAgents = !string.Equals(mode, "shared", StringComparison.OrdinalIgnoreCase),
        };
    }
}
