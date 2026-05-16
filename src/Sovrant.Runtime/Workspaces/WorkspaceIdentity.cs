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
    /// Format: <c>ws-personal-{sanitized-userId}</c>.
    /// Strips characters that are invalid in Windows filenames so the ID is safe as a directory name.
    /// </summary>
    public static string DefaultPersonalFor(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId must be non-empty.", nameof(userId));
        return PersonalPrefix + SanitizeForPath(userId);
    }

    /// <summary>
    /// Strips or replaces characters that are illegal in Windows filenames or NTFS paths.
    /// Keeps alphanumerics, hyphens, underscores, and dots; replaces everything else with a hyphen.
    /// </summary>
    public static string SanitizeForPath(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var chars = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            chars[i] = char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '-';
        }
        // Trim trailing dots/spaces (illegal on Windows) and collapse runs of hyphens.
        var result = new string(chars).Trim('-').Trim('.');
        return string.IsNullOrEmpty(result) ? "user" : result;
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
