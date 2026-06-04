namespace Sovrant.Runtime.Knowledge;

/// <summary>A single attribution row — which knowledge item was used in which session turn.</summary>
public sealed record KnowledgeAttribution(
    string SessionId,
    int TurnIndex,
    string Kind,
    string Slug,
    DateTimeOffset UsedAt);

/// <summary>
/// Phase 116 F — append-only store for knowledge attribution rows.
/// One row per (session, turn, kind, slug) combination; duplicates within a
/// turn are permitted (the slug was invoked more than once).
/// </summary>
public interface IKnowledgeAttributionStore
{
    Task RecordAsync(string sessionId, int turnIndex, string kind, string slug, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeAttribution>> GetBySessionAsync(string sessionId, CancellationToken ct = default);
}
