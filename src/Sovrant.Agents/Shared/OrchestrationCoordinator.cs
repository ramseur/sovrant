using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Config;
using Sovrant.Agents.Models;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Agents.Shared;

/// <summary>
/// Routes tasks to registered agents and manages the task lifecycle for
/// <see cref="InProcessOrchestrationSystem"/>. Each task gets its own linked
/// <see cref="CancellationTokenSource"/> so individual tasks can be cancelled
/// without affecting others.
/// </summary>
public sealed partial class OrchestrationCoordinator : IDisposable
{
    private readonly ConcurrentDictionary<string, IAgent> _agents =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCts =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _semaphore;
    private readonly ILiveSettings<AgentSystemConfig> _config;
    private readonly ILogger<OrchestrationCoordinator> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatching task '{TaskId}' to agent '{AgentName}'")]
    private static partial void LogDispatch(ILogger logger, string taskId, string agentName);

    /// <summary>
    /// DI ctor — accepts the hot-reloadable <see cref="ILiveSettings{AgentSystemConfig}"/>.
    /// Tests with a static <see cref="AgentSystemConfig"/> wrap it in
    /// <see cref="LiveSettings.Static{T}"/>.
    /// </summary>
    public OrchestrationCoordinator(
        ILiveSettings<AgentSystemConfig> config,
        ILogger<OrchestrationCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);
        _config = config;
        _logger = logger;
        // Concurrency cap is captured at construction — a hot-reload of
        // MaxConcurrentAgents takes effect on the next process start. Timeout
        // (TaskTimeoutSeconds) is read live on each Dispatch.
        var initial = config.Current;
        _semaphore = new SemaphoreSlim(initial.MaxConcurrentAgents, initial.MaxConcurrentAgents);
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
            TimeSpan.FromSeconds(_config.Current.TaskTimeoutSeconds));
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
            if (_taskCts.TryRemove(task.Id, out var removed))
                removed.Dispose();
        }
    }

    /// <summary>Cancels an in-flight task by its identifier. No-op if not found.</summary>
    public void Cancel(string taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        if (_taskCts.TryGetValue(taskId, out var cts))
            cts.Cancel();
    }

    /// <summary>Cancels all in-flight tasks.</summary>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var cts in _taskCts.Values)
            await cts.CancelAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => _semaphore.Dispose();
}
