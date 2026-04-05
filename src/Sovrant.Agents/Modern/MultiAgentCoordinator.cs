using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Config;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Modern;

/// <summary>
/// Routes tasks to registered agents and manages the task lifecycle for
/// <see cref="InProcessMultiAgentSystem"/>. Each task gets its own linked
/// <see cref="CancellationTokenSource"/> so individual tasks can be cancelled
/// without affecting others.
/// </summary>
public sealed partial class MultiAgentCoordinator : IDisposable
{
    private readonly Dictionary<string, IAgent> _agents =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCts =
        new(StringComparer.Ordinal);
    private readonly ConcurrentBag<Task> _agentLoopTasks = [];
    private readonly SemaphoreSlim _semaphore;
    private readonly AgentSystemConfig _config;
    private readonly ILogger<MultiAgentCoordinator> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatching task '{TaskId}' to agent '{AgentName}'")]
    private static partial void LogDispatch(ILogger logger, string taskId, string agentName);

    public MultiAgentCoordinator(AgentSystemConfig config, ILogger<MultiAgentCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);
        _config = config;
        _logger = logger;
        _semaphore = new SemaphoreSlim(config.MaxConcurrentAgents, config.MaxConcurrentAgents);
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
    /// the coordinator falls back to the first registered agent.
    /// </summary>
    public async Task<AgentResult> DispatchAsync(AgentTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        // Resolve target agent
        IAgent? agent = null;
        if (task.AssignedAgentName is not null)
        {
            if (!_agents.TryGetValue(task.AssignedAgentName, out agent))
                return AgentResult.Fail(task.Id, $"Unknown agent: '{task.AssignedAgentName}'.");
        }
        else
        {
            // Fall back to first registered agent
            agent = _agents.Values.FirstOrDefault();
            if (agent is null)
                return AgentResult.Fail(task.Id, "No agents registered.");
        }

        // Create linked CTS: caller's ct + timeout
        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(_config.TaskTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        _taskCts[task.Id] = linkedCts;
        try
        {
            await _semaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            try
            {
                LogDispatch(_logger, task.Id, agent.Name);
                return await agent.HandleAsync(task, linkedCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return AgentResult.Fail(task.Id,
                timeoutCts.IsCancellationRequested
                    ? "Task timed out."
                    : "Task was cancelled.");
        }
        finally
        {
            _taskCts.TryRemove(task.Id, out _);
        }
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

        // Wait for all tracked agent run-loop tasks to complete.
        if (!_agentLoopTasks.IsEmpty)
        {
            try
            {
                await Task.WhenAll(_agentLoopTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }
    }

    /// <summary>Tracks an agent's <see cref="BaseAgent.RunLoopAsync"/> task for shutdown awaiting.</summary>
    internal void TrackAgentLoop(Task loopTask) => _agentLoopTasks.Add(loopTask);

    /// <inheritdoc/>
    public void Dispose() => _semaphore.Dispose();
}
