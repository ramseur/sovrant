namespace Sovrant.Runtime.Users;

/// <summary>
/// Phase 37 — User management API surface. CRUD over the Phase 32 <c>users</c>
/// table plus per-user data views (sessions, usage, audit). All identity is
/// still gated by the single <c>SOVRANT_TOKEN</c>; per-user authentication is
/// deferred to Phase 38.
/// </summary>
public interface IUserService
{
    // ── CRUD ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a user. The <paramref name="userId"/> is server-generated when omitted
    /// (preferred). Validates <paramref name="username"/> against the slug pattern
    /// and the optional <paramref name="email"/> against a basic shape check.
    /// </summary>
    /// <exception cref="ArgumentException">Validation failed.</exception>
    /// <exception cref="Microsoft.Data.Sqlite.SqliteException">
    /// Unique violation (username or email already exists). Surfaced to the caller
    /// so the route layer can map to <c>409 Conflict</c>.
    /// </exception>
    Task<User> CreateAsync(
        string username,
        string? email = null,
        string role = "user",
        string? team = null,
        string? userId = null,
        CancellationToken ct = default);

    /// <summary>Gets a user by ID. Returns <c>null</c> when not found.</summary>
    Task<User?> GetAsync(string userId, CancellationToken ct = default);

    /// <summary>Gets a user by username. Returns <c>null</c> when not found.</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Gets the user profile (user + derived stats).</summary>
    Task<UserProfile?> GetProfileAsync(string userId, CancellationToken ct = default);

    /// <summary>Lists users with optional filters.</summary>
    Task<IReadOnlyList<User>> ListAsync(UserListFilter? filter = null, CancellationToken ct = default);

    /// <summary>Counts users matching the filter (for pagination).</summary>
    Task<int> CountAsync(UserListFilter? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Updates one or more mutable fields. Pass <c>null</c> to leave a field unchanged.
    /// Server-controlled fields (<c>user_id</c>, <c>created_at</c>) are never modified.
    /// </summary>
    /// <exception cref="ArgumentException">Validation failed.</exception>
    Task<User?> UpdateAsync(
        string userId,
        string? username = null,
        string? email = null,
        string? role = null,
        string? team = null,
        string? status = null,
        CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a user by setting <c>status='inactive'</c>. The row is preserved
    /// so all FK references (workspaces, projects, sessions, audit) remain valid.
    /// Hard-delete is intentionally not exposed.
    /// </summary>
    /// <returns><c>true</c> if the user existed and is now inactive; <c>false</c> if not found.</returns>
    Task<bool> DeactivateAsync(string userId, CancellationToken ct = default);

    /// <summary>Re-activates a previously soft-deleted user.</summary>
    Task<bool> ReactivateAsync(string userId, CancellationToken ct = default);

    // ── Per-user data views ────────────────────────────────────────────────

    /// <summary>Lists session IDs owned by a user, newest first.</summary>
    Task<IReadOnlyList<string>> ListSessionsAsync(string userId, int limit = 100, int offset = 0, CancellationToken ct = default);

    /// <summary>Aggregated token usage for a user, optionally filtered by model and date range.</summary>
    Task<UserUsage> GetUsageAsync(
        string userId,
        string? model = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);

    /// <summary>
    /// Audit events attributed to a user. Joined via <c>sessions.user_id</c> because
    /// <c>audit_governance</c> tracks <c>session_id</c> rather than <c>user_id</c>.
    /// </summary>
    Task<IReadOnlyList<UserAuditEvent>> ListAuditEventsAsync(string userId, int limit = 100, CancellationToken ct = default);
}
