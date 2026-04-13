using System.Text;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Templates;

namespace Sovrant.Agents.Coordination;

/// <summary>
/// Phase 57 — LLM-backed PM agent that uses a <see cref="SovrantAgent"/> to
/// decide what inter-group coordination events to emit. Accumulates a
/// briefing buffer of received events that feeds into the broadcast decision.
/// </summary>
public sealed partial class LlmPMAgent : IPMAgent
{
    private readonly string _groupId;
    private readonly string _workspaceId;
    private readonly string? _projectId;
    private readonly SovrantAgent _agent;
    private readonly ILogger _logger;
    private readonly List<string> _briefingBuffer = [];
    private readonly object _bufferLock = new();

    [LoggerMessage(Level = LogLevel.Debug, Message = "PM agent for group '{GroupId}': processing update from '{AgentName}'")]
    private static partial void LogUpdate(ILogger logger, string groupId, string agentName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "PM agent for group '{GroupId}': received {Count} coordination events")]
    private static partial void LogReceived(ILogger logger, string groupId, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "PM agent for group '{GroupId}': deciding broadcast from {BufferSize} buffered items")]
    private static partial void LogBroadcast(ILogger logger, string groupId, int bufferSize);

    public LlmPMAgent(
        string groupId,
        string workspaceId,
        string? projectId,
        SovrantAgent agent,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(logger);
        _groupId = groupId;
        _workspaceId = workspaceId;
        _projectId = projectId;
        _agent = agent;
        _logger = logger;
    }

    public string GroupId => _groupId;

    public async Task<IReadOnlyList<CoordinationEvent>> OnGroupUpdateAsync(
        string agentName,
        string updateSummary,
        CancellationToken ct = default)
    {
        LogUpdate(_logger, _groupId, agentName);

        var prompt = $"Agent '{agentName}' in your group '{_groupId}' reports:\n{updateSummary}\n\n" +
                     "Should any other groups be notified? " +
                     "Respond with NONE if no coordination is needed, or describe what to communicate and to which group.";

        var task = AgentTask.Create(prompt, _agent.Name);
        var result = await _agent.HandleAsync(task, ct).ConfigureAwait(false);

        // For now, the LLM response is informational; actual event creation
        // happens via DecideBroadcastAsync at wave boundaries.
        if (result.Success && !result.Output.Contains("NONE", StringComparison.OrdinalIgnoreCase))
        {
            lock (_bufferLock)
            {
                _briefingBuffer.Add($"[Update from {agentName}]: {updateSummary}");
            }
        }

        return [];
    }

    public Task ReceiveCoordinationAsync(
        IReadOnlyList<CoordinationEvent> events,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        LogReceived(_logger, _groupId, events.Count);

        lock (_bufferLock)
        {
            foreach (var evt in events)
            {
                _briefingBuffer.Add(
                    $"[From {evt.SourceGroupId}] {evt.EventType}: {evt.Payload}");
            }
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<CoordinationEvent>> DecideBroadcastAsync(
        CancellationToken ct = default)
    {
        string[] bufferSnapshot;
        lock (_bufferLock)
        {
            if (_briefingBuffer.Count == 0)
                return [];

            bufferSnapshot = [.. _briefingBuffer];
            _briefingBuffer.Clear();
        }

        LogBroadcast(_logger, _groupId, bufferSnapshot.Length);

        var sb = new StringBuilder();
        sb.Append("You are the PM for group '").Append(_groupId).AppendLine("'. Here is your briefing:");
        foreach (var item in bufferSnapshot)
        {
            sb.Append("- ").AppendLine(item);
        }
        sb.AppendLine();
        sb.AppendLine("Based on this briefing, decide what coordination events to broadcast.");
        sb.AppendLine("For each event, specify: TARGET_GROUP, EVENT_TYPE (Blocker|Update|Request|Handoff), and a summary.");
        sb.AppendLine("If no coordination is needed, respond with NONE.");

        var task = AgentTask.Create(sb.ToString(), _agent.Name);
        var result = await _agent.HandleAsync(task, ct).ConfigureAwait(false);

        // Parse the LLM response — for Phase 57 we keep it simple and don't
        // attempt structured extraction. The coordinator will handle routing.
        if (!result.Success || result.Output.Contains("NONE", StringComparison.OrdinalIgnoreCase))
            return [];

        // Return a single Update event summarising the PM's broadcast decision.
        // Future phases can add structured parsing of multiple events.
        return
        [
            new CoordinationEvent(
                SourceGroupId: _groupId,
                TargetGroupId: "*",  // broadcast to all registered groups
                EventType: CoordinationEventType.Update,
                Payload: result.Output,
                WorkspaceId: _workspaceId,
                ProjectId: _projectId),
        ];
    }
}
