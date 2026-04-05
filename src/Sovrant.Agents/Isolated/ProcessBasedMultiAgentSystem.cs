using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Config;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Isolated;

/// <summary>
/// Multi-agent backend that spawns a separate process per agent. Agents communicate via
/// stdin/stdout and output is streamed incrementally. Tool-use messages are parsed in the
/// same format as the original OpenClaude process-based agent protocol.
/// <para>
/// This is the default backend. Activate via <c>AGENT_MODE=isolated</c> (or by omitting
/// <c>AGENT_MODE</c> entirely) or
/// <see cref="AgentSystemConfig.UseIsolatedAgents"/> = <see langword="true"/>.
/// </para>
/// </summary>
public sealed partial class ProcessBasedMultiAgentSystem : IMultiAgentSystem, IDisposable
{
    private readonly Dictionary<string, ProcessAgent> _agents =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCts =
        new(StringComparer.Ordinal);
    private readonly AgentSystemConfig _config;
    private readonly ILogger<ProcessBasedMultiAgentSystem> _logger;
    private volatile bool _disposed;

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatching task '{TaskId}' to process agent '{AgentName}'")]
    private static partial void LogDispatch(ILogger logger, string taskId, string agentName);

    public ProcessBasedMultiAgentSystem(AgentSystemConfig config, ILogger<ProcessBasedMultiAgentSystem> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="agent"/> is not a <see cref="ProcessAgent"/>.
    /// </exception>
    public void RegisterAgent(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (agent is not ProcessAgent processAgent)
            throw new ArgumentException(
                $"{nameof(ProcessBasedMultiAgentSystem)} only accepts {nameof(ProcessAgent)} instances.",
                nameof(agent));
        _agents[agent.Name] = processAgent;
    }

    /// <inheritdoc/>
    public async Task<AgentResult> RunTaskAsync(AgentTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Resolve target agent
        ProcessAgent? agent = null;
        if (task.AssignedAgentName is not null)
        {
            if (!_agents.TryGetValue(task.AssignedAgentName, out agent))
                return AgentResult.Fail(task.Id, $"Unknown agent: '{task.AssignedAgentName}'.");
        }
        else
        {
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
            LogDispatch(_logger, task.Id, agent.Name);
            return await agent.HandleAsync(task, linkedCts.Token).ConfigureAwait(false);
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

    /// <inheritdoc/>
    public void CancelTask(string taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        if (_taskCts.TryGetValue(taskId, out var cts))
            cts.Cancel();
    }

    /// <inheritdoc/>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        foreach (var cts in _taskCts.Values)
            await cts.CancelAsync().ConfigureAwait(false);

        // In-flight process agents will be killed by their linked CTS cancellation.
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        foreach (var cts in _taskCts.Values)
            cts.Dispose();
        _taskCts.Clear();
    }
}
