using System.Text.RegularExpressions;

namespace Sovrant.Server;

/// <summary>
/// Shared input validation helpers for HTTP-facing routes. Validates user-supplied
/// strings (session IDs, model names, etc.) before they reach domain logic that may
/// use them in file paths or external API calls.
/// </summary>
internal static partial class InputValidation
{
    // Session IDs: 1-128 chars, alphanumeric + hyphens + underscores + colons + dots.
    [GeneratedRegex(@"^[a-zA-Z0-9._:@\-]{1,128}$")]
    private static partial Regex SessionIdPattern();

    // Model names: 1-128 chars, alphanumeric + hyphens + underscores + dots + slashes + colons.
    [GeneratedRegex(@"^[a-zA-Z0-9._/:\-]{1,128}$")]
    private static partial Regex ModelNamePattern();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="sessionId"/> is safe to use as
    /// a filesystem key and pool key. Rejects path traversal characters, whitespace,
    /// and excessively long values.
    /// </summary>
    public static bool IsValidSessionId(string? sessionId) =>
        sessionId is not null && SessionIdPattern().IsMatch(sessionId);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="model"/> is a safe model identifier.
    /// </summary>
    public static bool IsValidModelName(string? model) =>
        model is not null && ModelNamePattern().IsMatch(model);

    // Resource IDs (workspace, project, user): 1-256 chars, same charset as session IDs.
    [GeneratedRegex(@"^[a-zA-Z0-9._:@\-]{1,256}$")]
    private static partial Regex ResourceIdPattern();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="id"/> is a safe resource identifier
    /// (workspace ID, project ID, user ID). Rejects path traversal, whitespace, and oversized values.
    /// </summary>
    public static bool IsValidResourceId(string? id) =>
        id is not null && ResourceIdPattern().IsMatch(id);
}
