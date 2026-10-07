using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Phase 145 A8.1 — holds every swarm to the admin's run time limit. A swarm started from a chat reply
/// is also inside that reply's deadline; this covers swarms started directly (Server API).
/// </summary>
public sealed class TimeLimitedSwarmOrchestrator(ISwarmOrchestrator inner, IWorkspaceSettingsStore? settings, TimeProvider? clock = null)
    : ISwarmOrchestrator
{
    public async Task<SwarmResult> ExecuteAsync(SwarmPlan plan, SwarmConfig config, Action<SwarmEvent>? onEvent = null,
        SwarmExecutionContext? executionContext = null, CancellationToken ct = default)
    {
        var limit = RunLimits.Load(settings);
        using var timer = new CancellationTokenSource(limit, clock ?? TimeProvider.System);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(ct, timer.Token);
        // A8.3: a security sign-out of whoever started it stops the swarm.
        using var running = Sovrant.Runtime.Auth.RunningWork.Track(
            SessionContext.Current?.OwnerUserId ?? Sovrant.Runtime.Auth.AmbientPrincipal.Current?.UserId, limited);
        try
        {
            return await inner.ExecuteAsync(plan, config, onEvent, executionContext, limited.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (running.StoppedBySignOut && !ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(Sovrant.Runtime.Auth.RunningWork.StoppedMessage, ex);
        }
        catch (OperationCanceledException ex) when (timer.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException(RunLimits.StoppedMessage(limit), ex);
        }
    }
}
