namespace Sovrant.Runtime.Workspaces;

/// <summary>
/// Canonical helpers for deriving workspace identifiers. Use these instead of
/// inlining <c>$"ws-personal-{userId}"</c> so the format stays consistent
/// across the runtime, UI, migrations, and artifact scope defaults.
/// </summary>
public static class WorkspaceIdentity
{
    public const string PersonalPrefix = "ws-personal-";

    /// <summary>
    /// Returns the canonical personal workspace id for the given user.
    /// Format: <c>ws-personal-{userId}</c>.
    /// </summary>
    public static string DefaultPersonalFor(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId must be non-empty.", nameof(userId));
        return PersonalPrefix + userId;
    }

    /// <summary>
    /// Resolves the active user id from <c>SOVRANT_USER_ID</c>, falling back
    /// to <see cref="Environment.UserName"/>. Mirrors the convention used by
    /// the Desktop, Web, and Server hosts.
    /// </summary>
    public static string CurrentUserId
        => Environment.GetEnvironmentVariable("SOVRANT_USER_ID") is { Length: > 0 } id
            ? id
            : Environment.UserName;

    /// <summary>
    /// Returns the canonical personal workspace id for the active user
    /// (resolved via <see cref="CurrentUserId"/>).
    /// </summary>
    public static string DefaultPersonal()
        => DefaultPersonalFor(CurrentUserId);

    public static bool IsPersonal(string workspaceId)
        => !string.IsNullOrEmpty(workspaceId) && workspaceId.StartsWith(PersonalPrefix, StringComparison.Ordinal);

    /// <summary>
    /// True if the given id is the legacy <c>"personal"</c> sentinel that
    /// predates the Phase 87 unification. Migrations rewrite these to
    /// <see cref="DefaultPersonalFor"/>.
    /// </summary>
    public static bool IsLegacyPersonalSentinel(string workspaceId)
        => string.Equals(workspaceId, "personal", StringComparison.Ordinal);
}
