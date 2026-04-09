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
}
