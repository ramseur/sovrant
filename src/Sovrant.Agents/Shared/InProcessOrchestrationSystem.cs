using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Shared;

/// <summary>
/// Orchestration backend that runs agents as in-memory objects connected by async message
/// channels. Tasks are routed by <see cref="OrchestrationCoordinator"/>. All agents share a
/// <see cref="WorkspaceContext"/> for inter-agent state.
/// <para>
/// Activate via <c>AGENT_MODE=shared</c> or
/// <see cref="Config.AgentSystemConfig.UseIsolatedAgents"/> = <see langword="false"/>.
/// </para>
/// </summary>
public sealed class InProcessOrchestrationSystem : IOrchestrationSystem, IAsyncDisposable
{
    private readonly OrchestrationCoordinator _coordinator;
    private readonly ILogger<InProcessOrchestrationSystem> _logger;

    /// <summary>The shared workspace visible to all registered agents.</summary>
    public WorkspaceContext Workspace { get; }

    public InProcessOrchestrationSystem(
        OrchestrationCoordinator coordinator,
        WorkspaceContext workspace,
        ILogger<InProcessOrchestrationSystem> logger)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(logger);
        _coordinator = coordinator;
        Workspace = workspace;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void RegisterAgent(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _coordinator.AddAgent(agent);
    }

    /// <inheritdoc/>
    public Task<AgentResult> RunTaskAsync(AgentTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        return _coordinator.DispatchAsync(task, ct);
    }

    /// <inheritdoc/>
    public void CancelTask(string taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        _coordinator.Cancel(taskId);
    }

    /// <inheritdoc/>
    public Task ShutdownAsync(CancellationToken ct = default) =>
        _coordinator.ShutdownAsync(ct);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
        _coordinator.Dispose();
    }
}
