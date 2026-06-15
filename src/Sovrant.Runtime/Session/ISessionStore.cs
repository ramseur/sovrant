namespace Sovrant.Runtime.Session;

/// <summary>
/// Persists conversation session entries to durable storage.
///
/// <para>Phase 38 — all reads/writes take an optional <c>ownerUserId</c>. When
/// non-null, the store MUST enforce that the caller owns the session (queries
/// filter by <c>owner_user_id</c>; mismatches are treated as not-found).
/// When null, the call is unfiltered — reserved for admin and system paths.</para>
///
/// <para>The SQLite implementation is the authoritative owner-check. The JSONL
/// fallback does not track ownership and only honours the filter when the
/// DualWrite primary has already enforced it.</para>
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Appends a single entry to the session log.
    /// <paramref name="ownerUserId"/> is recorded on the <c>sessions</c> row
    /// the first time a given session_id is seen. On subsequent appends the
    /// owner is not overwritten.
    /// </summary>
    Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Loads all entries for the given session in order. If <paramref name="ownerUserId"/>
    /// is non-null and does not match the recorded owner, returns an empty list.
    /// </summary>
    Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Lists all session IDs that have at least one entry. If
    /// <paramref name="ownerUserId"/> is non-null, only sessions owned by
    /// that user are returned.
    /// </summary>
    Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a single session and all its entries. If
    /// <paramref name="ownerUserId"/> is non-null and does not match the
    /// recorded owner, returns <c>false</c> and nothing is deleted.
    /// </summary>
    Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>Deletes all sessions and their entries. Admin-only — has no owner filter.</summary>
    Task<int> DeleteAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the recorded owner user_id for the given session, or <c>null</c>
    /// if the session is unknown. Used by pool and route handlers to perform
    /// pre-flight ownership checks without loading entry data.
    /// </summary>
    Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Sets or updates the title for a session.</summary>
    Task SetTitleAsync(string sessionId, string title, string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>Returns the title for a session, or <c>null</c> if unset.</summary>
    Task<string?> GetTitleAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Lists sessions with titles and timestamps, ordered by most recently updated.
    /// </summary>
    Task<IReadOnlyList<SessionListItem>> ListWithTitlesAsync(string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Searches session content using FTS5 full-text search. Returns sessions
    /// whose entries match the query, ordered by relevance. If
    /// <paramref name="ownerUserId"/> is non-null, only sessions owned by
    /// that user are returned.
    /// </summary>
    Task<IReadOnlyList<SessionListItem>> SearchAsync(string query, string? ownerUserId = null, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Returns the names of MCP servers whose tools are exposed for this session,
    /// or <c>null</c> if no gating is set (every connected server is available).
    /// </summary>
    Task<IReadOnlyList<string>?> GetMcpConnectionsAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the per-session MCP connection allow-list. Pass <c>null</c> to clear
    /// the gate (every connected server is exposed); pass an empty list to disable
    /// all MCP tools for the session.
    /// </summary>
    Task SetMcpConnectionsAsync(string sessionId, IReadOnlyList<string>? servers, string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Phase 99 — flips the privacy flag on a session. The owner predicate
    /// is enforced inside the UPDATE so a mismatched owner is a silent
    /// no-op (zero rows affected) and never leaks the session's existence.
    /// </summary>
    Task UpdatePrivacyAsync(string sessionId, string ownerUserId, bool isPrivate, CancellationToken ct = default);

    /// <summary>
    /// Phase 99 — returns the current privacy flag for the session, or
    /// <c>null</c> when the session has no row yet (which the UI treats
    /// as "use the default", currently private).
    /// </summary>
    Task<bool?> GetIsPrivateAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Phase 106 — records the agent name bound to this session (set on first
    /// message; owner-scoped so a mismatch is a silent no-op).
    /// </summary>
    Task SetAgentNameAsync(string sessionId, string agentName, string? ownerUserId = null, CancellationToken ct = default);

    /// <summary>
    /// Phase 106 — returns the agent name stored for this session, or <c>null</c>
    /// if the session was started without an agent scope.
    /// </summary>
    Task<string?> GetAgentNameAsync(string sessionId, CancellationToken ct = default);
}
