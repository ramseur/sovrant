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
    /// </summary>
    /// <param name="sessionId">The session identifier (may be a composite key like <c>session::provider</c>).</param>
    /// <param name="scopedRouterOverride">
    /// When not <see langword="null"/>, the new runtime is wired to this router instead of the
    /// DI-registered <see cref="Sovrant.Api.Routing.ISmartRouter"/>. Used for per-request credential overrides.
    /// Ignored if the session already exists in the pool.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    Task<IConversationRuntime> GetOrCreateAsync(
        string sessionId,
        Sovrant.Api.Routing.ISmartRouter? scopedRouterOverride = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the runtime for <paramref name="sessionId"/> from the pool.
    /// Call this when a session is deleted so stale state is not retained.
    /// </summary>
    void Evict(string sessionId);
}
