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
    /// <param name="ct">A cancellation token.</param>
    Task<PooledSession> GetOrCreateAsync(
        string sessionId,
        Sovrant.Api.Routing.ISmartRouter? scopedRouterOverride = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the runtime for <paramref name="sessionId"/> from the pool and disposes its lock.
    /// Call this when a session is deleted so stale state is not retained.
    /// </summary>
    void Evict(string sessionId);

    /// <summary>Returns the number of active sessions in the pool.</summary>
    int ActiveCount { get; }

    /// <summary>
    /// Evicts sessions that have been idle longer than <paramref name="ttl"/>,
    /// and enforces the <paramref name="maxSessions"/> cap by evicting least-recently-used entries.
    /// Returns the number of sessions evicted.
    /// </summary>
    int EvictExpired(TimeSpan ttl, int maxSessions);
}

/// <summary>
/// A pooled session entry containing the runtime and a per-session lock for turn serialization.
/// Callers must acquire <see cref="Lock"/> before calling <see cref="Runtime"/>'s <c>RunTurnAsync</c>
/// and release it after, to prevent concurrent turns from corrupting the shared history.
/// </summary>
/// <param name="Runtime">The conversation runtime for this session.</param>
/// <param name="Lock">A semaphore (max 1) for serializing turns within this session.</param>
public sealed record PooledSession(IConversationRuntime Runtime, SemaphoreSlim Lock);
