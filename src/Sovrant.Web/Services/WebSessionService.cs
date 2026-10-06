using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Sovrant.Runtime.Auth;
using Sovrant.Web.Auth;

namespace Sovrant.Web.Services;

/// <summary>
/// Who is signed in on this browser tab.
/// <para>
/// <b>Embedded mode (Phase 145):</b> one per Blazor circuit (scoped), read from the sign-in cookie's
/// principal, so each browser is its own user. Sign-in and sign-out happen through
/// <c>/auth/login</c> and <c>/auth/logout</c>, not through this class.
/// </para>
/// <para>
/// <b>Remote mode (until Phase 145 Part C):</b> still one process-wide user, set by
/// <see cref="SignIn"/> on the Login page.
/// </para>
/// Implements <see cref="IPrincipalAccessor"/> so components get the signed-in identity.
/// </summary>
public sealed class WebSessionService : IPrincipalAccessor
{
    private volatile string? _userId;
    private volatile string? _role;
    private volatile string? _email;
    private volatile string? _workspaceId;

    /// <summary>Remote mode: a process-wide holder filled in by <see cref="SignIn"/>.</summary>
    public WebSessionService() { }

    /// <summary>Embedded mode: this circuit's (or request's) signed-in user from the sign-in cookie.</summary>
    public WebSessionService(AuthenticationStateProvider? authState, IHttpContextAccessor? http)
    {
        var user = ReadUser(authState) ?? http?.HttpContext?.User;
        SignedOutReason = http?.HttpContext?.Items[WebAuth.SignedOutReasonItem] as string;
        if (user?.Identity?.IsAuthenticated != true)
            return;
        _userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        _role = user.FindFirstValue(ClaimTypes.Role);
        _email = user.FindFirstValue(ClaimTypes.Email);
        SignInId = user.FindFirstValue(WebAuth.SignInIdClaim);
        Remember = string.Equals(user.FindFirstValue(WebAuth.RememberClaim), "true", StringComparison.Ordinal);
        IsCookieSignIn = true;
    }

    public string? UserId => _userId;
    public string? Role => _role;
    public string? Email => _email;
    public bool IsAdmin => string.Equals(_role, "admin", StringComparison.OrdinalIgnoreCase);
    public string? WorkspaceId => _workspaceId;
    public bool IsAuthenticated => _userId is not null;

    /// <summary>This browser's sign-in (embedded mode), for the account menu and "This browser" in Admin → Users.</summary>
    public string? SignInId { get; }

    /// <summary>True when this browser chose "Keep me signed in".</summary>
    public bool Remember { get; }

    /// <summary>True when the identity came from the per-browser cookie (embedded mode).</summary>
    public bool IsCookieSignIn { get; }

    /// <summary>"timedout" or "revoked" when this request arrived with a cookie that was refused.</summary>
    public string? SignedOutReason { get; }

    /// <summary>Remote mode only (process-wide). Embedded mode signs in through <c>/auth/login</c>.</summary>
    public void SignIn(string userId, string role, string? email = null)
    {
        // Reset all state first so no previous user's context leaks into the new session.
        _workspaceId = null;
        _userId = userId;
        _role = role;
        _email = email;
    }

    /// <summary>Remote mode only. Embedded mode signs out through <c>/auth/logout</c>.</summary>
    public void SignOut()
    {
        _userId = null;
        _role = null;
        _email = null;
        _workspaceId = null;
    }

    public void SetWorkspace(string? workspaceId) => _workspaceId = workspaceId;

    /// <summary>
    /// Phase 145: this tab's sign-in has ended (timed out, revoked, signed out elsewhere). The tab stops
    /// acting as the user at once, even if the browser ignores the redirect to Sign in.
    /// </summary>
    public void MarkSignedOut(string? reason)
    {
        SignOut();
        EndedReason = reason;
    }

    /// <summary>Why this tab's sign-in ended while it was open, if it did.</summary>
    public string? EndedReason { get; private set; }

    // The circuit's authentication state is set before any component renders, so the task has
    // completed; it isn't available at all for plain HTTP endpoints, which fall back to HttpContext.User.
    private static ClaimsPrincipal? ReadUser(AuthenticationStateProvider? authState)
    {
        if (authState is null) return null;
        try
        {
            var task = authState.GetAuthenticationStateAsync();
            return task.IsCompletedSuccessfully ? task.Result.User : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
