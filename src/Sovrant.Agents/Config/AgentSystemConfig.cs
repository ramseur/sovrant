namespace Sovrant.Agents.Config;

/// <summary>
/// Configuration for the multi-agent system.
/// <para>
/// The simplest way to switch backends is the <c>AGENT_MODE</c> environment variable:
/// <list type="bullet">
///   <item><c>AGENT_MODE=legacy</c> — uses <see cref="Legacy.ProcessBasedMultiAgentSystem"/></item>
///   <item><c>AGENT_MODE=modern</c> (or unset) — uses <see cref="Modern.InProcessMultiAgentSystem"/> (default)</item>
/// </list>
/// Alternatively, set <see cref="UseLegacyAgents"/> programmatically before calling
/// <c>services.AddMultiAgentSystem(config)</c>.
/// </para>
/// </summary>
public sealed class AgentSystemConfig
{
    /// <summary>
    /// When <see langword="true"/>, uses <see cref="Legacy.ProcessBasedMultiAgentSystem"/>
    /// (process-per-agent, stdin/stdout). When <see langword="false"/> (default), uses
    /// <see cref="Modern.InProcessMultiAgentSystem"/> (in-process async channels).
    /// </summary>
    public bool UseLegacyAgents { get; init; }

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
    /// <c>AGENT_MODE=legacy</c> sets <see cref="UseLegacyAgents"/> = <see langword="true"/>;
    /// any other value (including absent) leaves the modern default.
    /// </summary>
    public static AgentSystemConfig FromEnvironment()
    {
        var mode = Environment.GetEnvironmentVariable("AGENT_MODE");
        return new AgentSystemConfig
        {
            UseLegacyAgents = string.Equals(mode, "legacy", StringComparison.OrdinalIgnoreCase),
        };
    }
}
