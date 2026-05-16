namespace Sovrant.Server.Auth;

/// <summary>
/// Canonical <see cref="HttpContext.Items"/> keys written by
/// <see cref="BearerTokenMiddleware"/> and read by downstream routes.
///
/// <para>Centralised so endpoints don't have to memorise magic strings — and
/// so a future rename only needs to touch one file.</para>
/// </summary>
internal static class SovrantHttpContextKeys
{
    /// <summary>The authenticated user id (e.g. <c>usr_a1b2c3...</c>).</summary>
    public const string UserId = "sovrant.user_id";

    /// <summary>The token id (e.g. <c>tok_...</c>) of the per-user token that authenticated this request.</summary>
    public const string TokenId = "sovrant.token_id";

    /// <summary>How the request was authenticated. Always <c>"token"</c> for a per-user <c>svt_*</c> token.</summary>
    public const string AuthMode = "sovrant.auth_mode";

    /// <summary>The owning user's role (e.g. <c>"user"</c> or <c>"admin"</c>).</summary>
    public const string Role = "sovrant.role";

    public const string AuthModeToken = "token";

    public const string RoleAdmin = "admin";

    /// <summary>
    /// The workspace ID resolved by <see cref="Sovrant.Server.Middleware.WorkspaceContextMiddleware"/>
    /// for this request. Present after middleware runs; absent for health/options requests.
    /// </summary>
    public const string WorkspaceId = "sovrant.workspace_id";
}
