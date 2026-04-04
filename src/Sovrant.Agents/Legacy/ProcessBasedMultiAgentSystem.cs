using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Legacy;

/// <summary>
/// Multi-agent backend that spawns a separate process per agent. Agents communicate via
/// stdin/stdout and output is streamed incrementally. Tool-use messages are parsed in the
/// same format as the original OpenClaude process-based agent protocol.
/// <para>
/// Activate via <c>AGENT_MODE=legacy</c> or
/// <see cref="Config.AgentSystemConfig.UseLegacyAgents"/> = <see langword="true"/>.
/// The modern in-process backend is preferred for new deployments.
/// </para>
/// </summary>
public sealed class ProcessBasedMultiAgentSystem : IMultiAgentSystem, IDisposable
{
    private readonly Dictionary<string, ProcessAgent> _agents =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCts =
        new(StringComparer.Ordinal);
    private readonly ILogger<ProcessBasedMultiAgentSystem> _logger;

    public ProcessBasedMultiAgentSystem(ILogger<ProcessBasedMultiAgentSystem> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
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
    /// <remarks>
    /// TODO (Phase 19): Resolve target agent by <see cref="AgentTask.AssignedAgentName"/> or
    /// fall back to the first registered agent; create a linked <see cref="CancellationTokenSource"/>
    /// for the task; spawn the process; stream and parse stdout; record the CTS for cancellation.
    /// </remarks>
    public Task<AgentResult> RunTaskAsync(AgentTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        throw new NotImplementedException(
            "Process-based multi-agent execution is not yet implemented. See Phase 19 roadmap.");
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

        // TODO (Phase 19): wait for in-flight process agents to drain stdout and exit gracefully.
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var cts in _taskCts.Values)
            cts.Dispose();
        _taskCts.Clear();
    }
}
