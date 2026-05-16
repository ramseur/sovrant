namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Maintains a pool of long-lived <see cref="IConversationRuntime"/> instances keyed by session ID.
/// A pooled runtime retains its in-memory message history across multiple HTTP requests,
/// giving the same continuity as an interactive CLI session.
/// </summary>
public interface IRuntimeSessionPool
{
    /// <summary>
    /// Returns the existing runtime for <paramref name="sessionId"/>, or creates and initialises
    /// a new one (replaying any persisted JSONL history) if none exists yet.
    /// The returned <see cref="PooledSession"/> includes a per-session lock for turn serialization.
    /// </summary>
    /// <param name="sessionId">The session identifier (may be a composite key like <c>session::provider</c>).</param>
    /// <param name="scopedRouterOverride">
    /// When not <see langword="null"/>, the new runtime is wired to this router instead of the
    /// DI-registered <see cref="Sovrant.Api.Routing.ISmartRouter"/>. Used for per-request credential overrides.
    /// Ignored if the session already exists in the pool.
    /// </param>
    /// <param name="ownerUserId">
    /// Authenticated user id that owns this session. Used both as an isolation key
    /// (two users requesting the same session_id get separate pool entries) and as
    /// the owner stamped on new rows in the SQLite store. Null means no enforcement
    /// (CLI, tests).
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    Task<PooledSession> GetOrCreateAsync(
        string sessionId,
        Sovrant.Api.Routing.ISmartRouter? scopedRouterOverride = null,
        string? ownerUserId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the runtime for <paramref name="sessionId"/> from the pool and disposes its lock.
    /// Call this when a session is deleted so stale state is not retained.
    /// When <paramref name="ownerUserId"/> is non-null the owner-partitioned pool entry is
    /// evicted; when null all entries for the given session id (regardless of owner) are
    /// evicted — used by admin flows.
    /// </summary>
    void Evict(string sessionId, string? ownerUserId = null);

    /// <summary>Returns the number of active sessions in the pool.</summary>
    int ActiveCount { get; }

    /// <summary>
    /// Evicts sessions that have been idle longer than <paramref name="ttl"/>,
    /// and enforces the <paramref name="maxSessions"/> cap by evicting least-recently-used entries.
    /// Returns the number of sessions evicted.
    /// </summary>
    int EvictExpired(TimeSpan ttl, int maxSessions);

    /// <summary>
    /// Returns the <see cref="SessionConfig"/> for the given session, or <see langword="null"/>
    /// if the session is not currently in the pool. When <paramref name="ownerUserId"/> is
    /// provided, only the owner-partitioned entry is returned; when null, any matching
    /// pool entry for the session id is returned (first by insertion order).
    /// </summary>
    SessionConfig? TryGetConfig(string sessionId, string? ownerUserId = null);
}

/// <summary>
/// A pooled session entry containing the runtime, a per-session lock, and session-scoped config.
/// Callers must acquire <see cref="Lock"/> before calling <see cref="Runtime"/>'s <c>RunTurnAsync</c>
/// and release it after, to prevent concurrent turns from corrupting the shared history.
/// </summary>
/// <param name="Runtime">The conversation runtime for this session.</param>
/// <param name="Lock">A semaphore (max 1) for serializing turns within this session.</param>
/// <param name="Config">Per-session config overlay (model, permission mode, token accumulators).</param>
public sealed record PooledSession(IConversationRuntime Runtime, SemaphoreSlim Lock, SessionConfig Config);
