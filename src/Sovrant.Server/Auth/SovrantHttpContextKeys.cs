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
    /// <summary>
    /// The authenticated user id (e.g. <c>usr_a1b2c3...</c>). Present for
    /// requests authenticated via a per-user <c>svt_*</c> token. Absent for
    /// requests authenticated via the legacy static <c>SOVRANT_TOKEN</c>.
    /// </summary>
    public const string UserId = "sovrant.user_id";

    /// <summary>
    /// The token id (e.g. <c>tok_...</c>) of the per-user token that
    /// authenticated this request. Absent for static-token requests.
    /// </summary>
    public const string TokenId = "sovrant.token_id";

    /// <summary>
    /// How the request was authenticated:
    /// <c>"token"</c> for a per-user <c>svt_*</c> token, or <c>"static"</c>
    /// for the legacy <c>SOVRANT_TOKEN</c> environment variable.
    /// </summary>
    public const string AuthMode = "sovrant.auth_mode";

    public const string AuthModeToken = "token";
    public const string AuthModeStatic = "static";
}
