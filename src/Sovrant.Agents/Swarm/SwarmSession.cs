using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Records swarm events to the SQLite-backed <see cref="ISwarmEventStore"/>
/// (Phase 37.5). Replays the same events for status queries and SSE catch-up.
/// </summary>
/// <remarks>
/// Before Phase 37.5 this class wrote JSONL files under
/// <c>~/.sovrant/swarm/sessions/{swarmId}.jsonl</c>. The public surface
/// (<see cref="RecordAsync"/>, <see cref="ReplayAsync"/>, <see cref="ListSessions"/>,
/// <see cref="Exists"/>) is unchanged so callers do not need updates — only
/// the underlying storage moved from a flat file to the <c>swarm_events</c>
/// table that has been waiting empty since V005.
/// </remarks>
public sealed class SwarmSession
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ISwarmEventStore _store;

    public SwarmSession(ISwarmEventStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>
    /// Records a single event for a swarm run. The optional
    /// <paramref name="context"/> stamps the row with workspace / project /
    /// agent identifiers so the event can later be filtered by scope.
    /// </summary>
    public async Task RecordAsync(
        SwarmEvent evt,
        SwarmExecutionContext? context = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var record = new SwarmEventRecord(
            SwarmId: evt.SwarmId,
            EventType: evt.GetType().Name,
            Payload: JsonSerializer.Serialize<object>(evt, s_jsonOptions),
            Timestamp: evt.Timestamp,
            AgentId: ExtractAgentName(evt),
            WorkspaceId: context?.WorkspaceId,
            ProjectId: context?.ProjectId);

        await _store.RecordEventAsync(record, ct).ConfigureAwait(false);
    }

    /// <summary>Replays all events from a swarm session as an async enumerable.</summary>
    public async IAsyncEnumerable<SwarmEvent?> ReplayAsync(
        string swarmId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var rows = await _store.LoadEventsAsync(swarmId, ct).ConfigureAwait(false);
        foreach (var row in rows)
        {
            SwarmEvent? evt = null;
            try
            {
                evt = DeserializeEvent(row.EventType, row.Payload);
            }
            catch (JsonException)
            {
                // Skip malformed payloads — preserves the legacy "be lenient on replay" behaviour.
            }

            if (evt is not null)
                yield return evt;
        }
    }

    /// <summary>
    /// Lists all swarm IDs known to the store, most-recently-active first.
    /// Optional filter narrows by workspace and/or project.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListSessionsAsync(SwarmListFilter? filter = null, CancellationToken ct = default)
    {
        return await _store.ListSwarmsAsync(filter, ct).ConfigureAwait(false);
    }

    /// <summary>Returns whether any events exist for the given swarm ID.</summary>
    public async Task<bool> ExistsAsync(string swarmId, CancellationToken ct = default)
    {
        return await _store.ExistsAsync(swarmId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Pulls the agent name out of an event when present, so it can be
    /// indexed into the <c>agent_id</c> column. Best-effort — many event
    /// types do not carry an agent name.
    /// </summary>
    private static string? ExtractAgentName(SwarmEvent evt) => evt switch
    {
        SwarmEvent.TaskStarted s => s.AgentName,
        SwarmEvent.TaskCompleted c => c.AgentName,
        SwarmEvent.TaskFailed f => f.AgentName,
        SwarmEvent.FileConflict fc => fc.AgentName,
        _ => null,
    };

    /// <summary>
    /// Reconstructs a strongly-typed <see cref="SwarmEvent"/> from the stored
    /// payload. The discriminator is the simple type name written by
    /// <see cref="RecordAsync"/>.
    /// </summary>
    private static SwarmEvent? DeserializeEvent(string eventType, string payload) => eventType switch
    {
        nameof(SwarmEvent.PlanCreated) => JsonSerializer.Deserialize<SwarmEvent.PlanCreated>(payload, s_jsonOptions),
        nameof(SwarmEvent.TaskStarted) => JsonSerializer.Deserialize<SwarmEvent.TaskStarted>(payload, s_jsonOptions),
        nameof(SwarmEvent.TaskCompleted) => JsonSerializer.Deserialize<SwarmEvent.TaskCompleted>(payload, s_jsonOptions),
        nameof(SwarmEvent.TaskFailed) => JsonSerializer.Deserialize<SwarmEvent.TaskFailed>(payload, s_jsonOptions),
        nameof(SwarmEvent.FileConflict) => JsonSerializer.Deserialize<SwarmEvent.FileConflict>(payload, s_jsonOptions),
        nameof(SwarmEvent.BudgetExceeded) => JsonSerializer.Deserialize<SwarmEvent.BudgetExceeded>(payload, s_jsonOptions),
        nameof(SwarmEvent.QualityGateStarted) => JsonSerializer.Deserialize<SwarmEvent.QualityGateStarted>(payload, s_jsonOptions),
        nameof(SwarmEvent.QualityGateCompleted) => JsonSerializer.Deserialize<SwarmEvent.QualityGateCompleted>(payload, s_jsonOptions),
        nameof(SwarmEvent.SwarmCompleted) => JsonSerializer.Deserialize<SwarmEvent.SwarmCompleted>(payload, s_jsonOptions),
        _ => null,
    };
}

/// <summary>
/// Identifies the user/workspace/project that owns a running swarm so that
/// every persisted event can be filtered by scope. Carried explicitly through
/// <see cref="SwarmOrchestrator.ExecuteAsync"/> so the call site is honest
/// about which row a swarm event will land under.
/// </summary>
public sealed record SwarmExecutionContext(
    string? UserId = null,
    string? WorkspaceId = null,
    string? ProjectId = null)
{
    /// <summary>An empty context — useful for tests and back-compat call sites.</summary>
    public static SwarmExecutionContext Empty { get; } = new();
}
