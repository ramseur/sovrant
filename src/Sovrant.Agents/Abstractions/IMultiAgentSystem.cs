using Sovrant.Agents.Models;

namespace Sovrant.Agents.Abstractions;

/// <summary>
/// A multi-agent coordination backend. Both <c>ProcessBasedMultiAgentSystem</c> (legacy)
/// and <c>InProcessMultiAgentSystem</c> (modern) implement this interface, making the
/// rest of the system backend-agnostic.
/// The active backend is selected at startup via <c>AGENT_MODE</c> or
/// <see cref="Config.AgentSystemConfig.UseLegacyAgents"/>.
/// </summary>
public interface IMultiAgentSystem
{
    /// <summary>Registers an agent with the system before any tasks are run.</summary>
    void RegisterAgent(IAgent agent);

    /// <summary>Routes a task to the appropriate agent and returns its result.</summary>
    Task<AgentResult> RunTaskAsync(AgentTask task, CancellationToken ct = default);

    /// <summary>Cancels an in-flight task by its identifier. No-op if the task is not found.</summary>
    void CancelTask(string taskId);

    /// <summary>Gracefully shuts down all agents and releases resources.</summary>
    Task ShutdownAsync(CancellationToken ct = default);
}
