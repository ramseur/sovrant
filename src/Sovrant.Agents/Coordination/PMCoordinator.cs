using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Coordination;

/// <summary>
/// Phase 57 — central router for inter-group coordination via PM agents.
/// </summary>
public interface IPMCoordinator
{
    /// <summary>Registers a group with its PM agent and mailbox.</summary>
    void RegisterGroup(string groupId, IPMAgent pmAgent, GroupMailbox mailbox);

    /// <summary>Unregisters a group, removing its PM agent and mailbox.</summary>
    void UnregisterGroup(string groupId);

    /// <summary>Routes a coordination event from source to target group, enforcing constraints.</summary>
    Task RouteEventAsync(CoordinationEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Triggers broadcast from a group's PM agent: drains incoming events,
    /// feeds them to the PM, and routes any outgoing events.
    /// </summary>
    Task BroadcastFromGroupAsync(string groupId, CancellationToken ct = default);

    /// <summary>Returns the number of pending coordination events for a group.</summary>
    Task<int> GetPendingCountAsync(string groupId, CancellationToken ct = default);
}

/// <summary>
/// Phase 57 — default implementation of <see cref="IPMCoordinator"/>.
/// Enforces same-workspace routing, no self-sends, and max routing depth.
/// </summary>
public sealed partial class PMCoordinator : IPMCoordinator
{
    private const int MaxRoutingDepth = 3;

    private readonly ConcurrentDictionary<string, IPMAgent> _pmAgents = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, GroupMailbox> _mailboxes = new(StringComparer.Ordinal);
    private readonly ILogger<PMCoordinator> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered PM agent for group '{GroupId}'")]
    private static partial void LogRegistered(ILogger logger, string groupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unregistered PM agent for group '{GroupId}'")]
    private static partial void LogUnregistered(ILogger logger, string groupId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Routing {EventType} from '{Source}' to '{Target}'")]
    private static partial void LogRouting(ILogger logger, CoordinationEventType eventType, string source, string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Routing rejected: self-send from '{GroupId}'")]
    private static partial void LogSelfSend(ILogger logger, string groupId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Routing rejected: target group '{GroupId}' not registered")]
    private static partial void LogTargetNotFound(ILogger logger, string groupId);

    public PMCoordinator(ILogger<PMCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public void RegisterGroup(string groupId, IPMAgent pmAgent, GroupMailbox mailbox)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(pmAgent);
        ArgumentNullException.ThrowIfNull(mailbox);

        _pmAgents[groupId] = pmAgent;
        _mailboxes[groupId] = mailbox;
        LogRegistered(_logger, groupId);
    }

    public void UnregisterGroup(string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        _pmAgents.TryRemove(groupId, out _);
        _mailboxes.TryRemove(groupId, out _);
        LogUnregistered(_logger, groupId);
    }

    public async Task RouteEventAsync(CoordinationEvent evt, CancellationToken ct = default)
    {
        await RouteEventCoreAsync(evt, depth: 0, ct).ConfigureAwait(false);
    }

    public async Task BroadcastFromGroupAsync(string groupId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        if (!_pmAgents.TryGetValue(groupId, out var pmAgent))
            return;
        if (!_mailboxes.TryGetValue(groupId, out var mailbox))
            return;

        // Drain incoming events and feed to PM
        var incoming = await mailbox.DrainPendingAsync(ct).ConfigureAwait(false);
        if (incoming.Count > 0)
        {
            await pmAgent.ReceiveCoordinationAsync(incoming, ct).ConfigureAwait(false);
        }

        // Let the PM decide what to broadcast
        var outgoing = await pmAgent.DecideBroadcastAsync(ct).ConfigureAwait(false);
        foreach (var evt in outgoing)
        {
            await RouteEventAsync(evt, ct).ConfigureAwait(false);
        }
    }

    public async Task<int> GetPendingCountAsync(string groupId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        if (_mailboxes.TryGetValue(groupId, out var mailbox))
            return await mailbox.PeekPendingCountAsync(ct).ConfigureAwait(false);

        return 0;
    }

    private async Task RouteEventCoreAsync(CoordinationEvent evt, int depth, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (depth >= MaxRoutingDepth)
            return;

        // No self-sends
        if (string.Equals(evt.SourceGroupId, evt.TargetGroupId, StringComparison.Ordinal))
        {
            LogSelfSend(_logger, evt.SourceGroupId);
            return;
        }

        // Broadcast to all groups (except source) when target is "*"
        if (evt.TargetGroupId == "*")
        {
            foreach (var (gid, mailbox) in _mailboxes)
            {
                if (string.Equals(gid, evt.SourceGroupId, StringComparison.Ordinal))
                    continue;

                var targeted = evt with { TargetGroupId = gid };
                LogRouting(_logger, targeted.EventType, targeted.SourceGroupId, targeted.TargetGroupId);
                await mailbox.PostAsync(targeted, ct).ConfigureAwait(false);
            }
            return;
        }

        // Direct routing to a specific group
        if (!_mailboxes.TryGetValue(evt.TargetGroupId, out var targetMailbox))
        {
            LogTargetNotFound(_logger, evt.TargetGroupId);
            return;
        }

        LogRouting(_logger, evt.EventType, evt.SourceGroupId, evt.TargetGroupId);
        await targetMailbox.PostAsync(evt, ct).ConfigureAwait(false);
    }
}
