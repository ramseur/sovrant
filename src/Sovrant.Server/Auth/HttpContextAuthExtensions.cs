namespace Sovrant.Server.Auth;

/// <summary>
/// Phase 38 — convenience accessors for the identity attached to a request
/// by <see cref="BearerTokenMiddleware"/>. Centralised so endpoints don't
/// have to read magic strings out of <see cref="HttpContext.Items"/> directly.
/// </summary>
internal static class HttpContextAuthExtensions
{
    /// <summary>
    /// The authenticated user id, or <c>null</c> if the request was
    /// authenticated via the legacy static <c>SOVRANT_TOKEN</c> path.
    /// </summary>
    public static string? GetUserId(this HttpContext ctx) =>
        ctx.Items[SovrantHttpContextKeys.UserId] as string;

    /// <summary>
    /// The token id (<c>tok_*</c>) used to authenticate this request, or
    /// <c>null</c> for static-token requests.
    /// </summary>
    public static string? GetTokenId(this HttpContext ctx) =>
        ctx.Items[SovrantHttpContextKeys.TokenId] as string;

    /// <summary>
    /// The owning user's role for per-user-token requests, or <c>null</c>
    /// for static-token requests. Use <see cref="IsAdmin"/> for the policy
    /// decision rather than comparing this string directly.
    /// </summary>
    public static string? GetRole(this HttpContext ctx) =>
        ctx.Items[SovrantHttpContextKeys.Role] as string;

    public static string? GetAuthMode(this HttpContext ctx) =>
        ctx.Items[SovrantHttpContextKeys.AuthMode] as string;

    /// <summary>
    /// Returns <c>true</c> for admin callers. Two paths grant admin:
    /// <list type="bullet">
    ///   <item>Legacy static <c>SOVRANT_TOKEN</c> requests — the bootstrap
    ///         god-token is intentionally privileged so CI scripts and the
    ///         local CLI keep working.</item>
    ///   <item>Per-user tokens whose owning user has
    ///         <c>users.role = 'admin'</c>.</item>
    /// </list>
    /// </summary>
    public static bool IsAdmin(this HttpContext ctx)
    {
        if (ctx.GetAuthMode() == SovrantHttpContextKeys.AuthModeStatic)
            return true;
        return ctx.GetRole() == SovrantHttpContextKeys.RoleAdmin;
    }

    /// <summary>
    /// Self-or-admin: an admin caller can act on any user; a non-admin
    /// caller can only act on their own row. Static-token callers always
    /// pass because <see cref="IsAdmin"/> is true for them.
    /// </summary>
    public static bool CanActOnUser(this HttpContext ctx, string targetUserId)
    {
        if (ctx.IsAdmin()) return true;
        var me = ctx.GetUserId();
        return me is not null && string.Equals(me, targetUserId, StringComparison.Ordinal);
    }
}
