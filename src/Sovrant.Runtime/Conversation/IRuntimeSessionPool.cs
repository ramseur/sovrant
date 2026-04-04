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
    Task<IConversationRuntime> GetOrCreateAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Removes the runtime for <paramref name="sessionId"/> from the pool.
    /// Call this when a session is deleted so stale state is not retained.
    /// </summary>
    void Evict(string sessionId);
}
