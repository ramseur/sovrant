using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Modern;

/// <summary>
/// Routes tasks to registered agents and manages the task lifecycle for
/// <see cref="InProcessMultiAgentSystem"/>. Each task gets its own linked
/// <see cref="CancellationTokenSource"/> so individual tasks can be cancelled
/// without affecting others.
/// </summary>
public sealed class MultiAgentCoordinator
{
    private readonly Dictionary<string, IAgent> _agents =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCts =
        new(StringComparer.Ordinal);
    private readonly ILogger<MultiAgentCoordinator> _logger;

    public MultiAgentCoordinator(ILogger<MultiAgentCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Registers an agent so it can receive dispatched tasks.</summary>
    public void AddAgent(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agents[agent.Name] = agent;
    }

    /// <summary>
    /// Dispatches <paramref name="task"/> to the target agent and returns its result.
    /// If <see cref="AgentTask.AssignedAgentName"/> is set, that agent is used; otherwise
    /// the coordinator selects a suitable agent by role or falls back to the first registered.
    /// </summary>
    /// <remarks>
    /// TODO (Phase 19): Create a linked CTS; enqueue the task to the target agent's inbox
    /// (if <see cref="BaseAgent"/>) or call <see cref="IAgent.HandleAsync"/> directly;
    /// await the result via a <c>TaskCompletionSource&lt;AgentResult&gt;</c> paired with the inbox;
    /// register and remove the CTS around the call.
    /// </remarks>
    public Task<AgentResult> DispatchAsync(AgentTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        // Touches _agents and _taskCts — instance access required once implemented.
        _ = _agents;
        _ = _taskCts;
        throw new NotImplementedException(
            "Task dispatch is not yet implemented. See Phase 19 roadmap.");
    }

    /// <summary>Cancels an in-flight task by its identifier. No-op if not found.</summary>
    public void Cancel(string taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        if (_taskCts.TryGetValue(taskId, out var cts))
            cts.Cancel();
    }

    /// <summary>
    /// Cancels all in-flight tasks and signals all <see cref="BaseAgent"/> instances
    /// to drain their inboxes and stop.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var cts in _taskCts.Values)
            await cts.CancelAsync().ConfigureAwait(false);

        foreach (var agent in _agents.Values)
        {
            if (agent is BaseAgent ba)
                ba.Complete();
        }

        // TODO (Phase 19): await all agent RunLoopAsync tasks before returning.
    }
}
