namespace Sovrant.Server.Auth;

/// <summary>
/// Convenience accessors for the identity attached to a request by
/// <see cref="BearerTokenMiddleware"/>. Centralised so endpoints don't have to
/// read magic strings out of <see cref="HttpContext.Items"/> directly.
/// </summary>
internal static class HttpContextAuthExtensions
{
    /// <summary>The authenticated user id.</summary>
    public static string? GetUserId(this HttpContext ctx) =>
        ctx.Items[SovrantHttpContextKeys.UserId] as string;

    /// <summary>The token id (<c>tok_*</c>) used to authenticate this request.</summary>
    public static string? GetTokenId(this HttpContext ctx) =>
        ctx.Items[SovrantHttpContextKeys.TokenId] as string;

    /// <summary>
    /// The owning user's role. Use <see cref="IsAdmin"/> for the policy
    /// decision rather than comparing this string directly.
    /// </summary>
    public static string? GetRole(this HttpContext ctx) =>
        ctx.Items[SovrantHttpContextKeys.Role] as string;

    public static string? GetAuthMode(this HttpContext ctx) =>
        ctx.Items[SovrantHttpContextKeys.AuthMode] as string;

    /// <summary>Returns <c>true</c> when the caller's user has <c>users.role = 'admin'</c>.</summary>
    public static bool IsAdmin(this HttpContext ctx) =>
        ctx.GetRole() == SovrantHttpContextKeys.RoleAdmin;

    /// <summary>
    /// Self-or-admin: an admin caller can act on any user; a non-admin
    /// caller can only act on their own row.
    /// </summary>
    public static bool CanActOnUser(this HttpContext ctx, string targetUserId)
    {
        if (ctx.IsAdmin()) return true;
        var me = ctx.GetUserId();
        return me is not null && string.Equals(me, targetUserId, StringComparison.Ordinal);
    }
}
