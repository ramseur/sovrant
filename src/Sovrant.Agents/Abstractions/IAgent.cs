using Sovrant.Agents.Models;

namespace Sovrant.Agents.Abstractions;

/// <summary>
/// An agent that handles tasks in a multi-agent system. Both isolated (process-based) and
/// shared (in-process) backends implement this interface.
/// </summary>
public interface IAgent
{
    /// <summary>Gets the unique name of this agent.</summary>
    string Name { get; }

    /// <summary>Handles a task and returns the result.</summary>
    Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default);
}
