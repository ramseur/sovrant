using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Shared;

/// <summary>
/// Base class for in-process agents. Subclasses override <see cref="HandleAsync"/> to
/// provide role-specific behaviour. The <see cref="OrchestrationCoordinator"/> calls
/// <see cref="HandleAsync"/> directly for each dispatched task.
/// </summary>
public abstract class BaseAgent : IAgent
{
    /// <summary>Logger available to all subclass implementations.</summary>
    protected ILogger Logger { get; }

    /// <param name="name">Unique agent name used for routing.</param>
    /// <param name="role">The functional role of this agent within a team.</param>
    /// <param name="logger">Logger for this agent instance.</param>
    protected BaseAgent(string name, AgentRole role, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(logger);
        Name = name;
        Role = role;
        Logger = logger;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>The functional role of this agent within an orchestrated team.</summary>
    public AgentRole Role { get; }

    /// <inheritdoc/>
    public abstract Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default);
}
