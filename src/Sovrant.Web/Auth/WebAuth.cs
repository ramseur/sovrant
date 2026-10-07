using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sovrant.Runtime;
using Sovrant.Runtime.Auth;

namespace Sovrant.Web.Auth;

/// <summary>
/// Phase 145 — per-browser sign-in for Sovrant.Web. The browser holds a random token in an HttpOnly
/// cookie; <see cref="IWebSignInService"/> stores its hash and decides whether it's still valid
/// (1 hour idle, 12 hours at most, 30 days with "Keep me signed in"; env-settable).
/// </summary>
public static class WebAuth
{
    public const string Scheme = "SovrantWeb";
    public const string CookieName = "sovrant_session";
    public const string SignInIdClaim = "sovrant:sign_in_id";
    public const string RememberClaim = "sovrant:remember";

    /// <summary>Set on the request when a presented cookie was refused: "timedout" or "revoked".</summary>
    public const string SignedOutReasonItem = "sovrant:signed_out_reason";

    public const string ReasonTimedOut = "timedout";
    public const string ReasonRevoked = "revoked";

    internal static ClaimsPrincipal ToPrincipal(WebSignIn signIn, string role)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, signIn.UserId),
            new Claim(ClaimTypes.Email, signIn.UserId), // since V043 the user id is the email
            new Claim(ClaimTypes.Role, role),
            new Claim(SignInIdClaim, signIn.SignInId),
            new Claim(RememberClaim, signIn.Remember ? "true" : "false"),
        ], Scheme);
        return new ClaimsPrincipal(identity);
    }

    internal static void WriteCookie(HttpContext ctx, string token, WebSignIn signIn) =>
        ctx.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            // A persistent cookie only for "Keep me signed in"; otherwise it ends with the browser.
            Expires = signIn.Remember ? signIn.ExpiresAt : null,
        });

    internal static void DeleteCookie(HttpContext ctx) =>
        ctx.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/", Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax });

    /// <summary>"Chrome on Windows"-style label for a User-Agent (see <see cref="WebSignInText"/>).</summary>
    public static string DescribeBrowser(string? userAgent) => WebSignInText.DescribeBrowser(userAgent);

    /// <summary>Adds the cookie scheme, the sign-in policy and authorization.</summary>
    public static IServiceCollection AddSovrantWebAuth(this IServiceCollection services)
    {
        services.AddSingleton(WebSignInPolicy.FromEnvironment(Environment.GetEnvironmentVariable));
        services.AddAuthentication(Scheme).AddScheme<AuthenticationSchemeOptions, WebAuthHandler>(Scheme, _ => { });
        services.AddAuthorization();
        services.AddCascadingAuthenticationState();
        return services;
    }

    /// <summary>
    /// POST /auth/login, /auth/register, /auth/logout and /auth/ping. The forms post here because a
    /// Blazor Server circuit (a WebSocket) can't set cookies.
    /// </summary>
    public static void MapSovrantWebAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async ([FromForm] SignInForm? form, HttpContext ctx, IIdentityService identity,
            IWebSignInService signIns, WebSignInPolicy policy, IServiceProvider services) =>
        {
            var result = await identity.LoginAsync(form?.Email ?? string.Empty, form?.Password ?? string.Empty, issueToken: false, ct: ctx.RequestAborted).ConfigureAwait(false);
            if (!result.Success || result.UserId is null)
                return Results.Redirect(LoginUrl(error: result.Error ?? "Sign-in failed.", email: form?.Email));
            return await SignInAsync(ctx, result.UserId, form?.IsRemember == true, signIns, policy, services).ConfigureAwait(false);
        });

        app.MapPost("/auth/register", async ([FromForm] SignInForm? form, HttpContext ctx, IIdentityService identity,
            IWebSignInService signIns, WebSignInPolicy policy, IServiceProvider services) =>
        {
            var result = await identity.RegisterAsync(form?.Email ?? string.Empty, form?.Password ?? string.Empty, issueToken: false, ct: ctx.RequestAborted).ConfigureAwait(false);
            if (!result.Success || result.UserId is null)
                return Results.Redirect(LoginUrl(error: result.Error ?? "Couldn't create the account.", email: form?.Email));
            if (result.IsPendingApproval)
                return Results.Redirect(LoginUrl(info: "approval", email: form?.Email));
            return await SignInAsync(ctx, result.UserId, form?.IsRemember == true, signIns, policy, services).ConfigureAwait(false);
        });

        app.MapPost("/auth/logout", async ([FromForm] SignOutForm? form, HttpContext ctx, IWebSignInService signIns) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var signInId = ctx.User.FindFirstValue(SignInIdClaim);
            if (userId is not null && string.Equals(form?.Scope, "all", StringComparison.Ordinal))
                await signIns.RevokeAllAsync(userId, WebSignInRevokeReasons.SignOutEverywhere, ct: ctx.RequestAborted).ConfigureAwait(false);
            else if (signInId is not null)
                await signIns.RevokeAsync(signInId, WebSignInRevokeReasons.SignOut, ctx.RequestAborted).ConfigureAwait(false);
            DeleteCookie(ctx);
            return Results.Redirect(LoginUrl(info: "signedout"));
        });

        // Activity ping from the page (at most once a minute, only after real keyboard/mouse activity):
        // authenticating the request already renewed the idle timeout. 401 tells the page to go to Sign in.
        app.MapPost("/auth/ping", (HttpContext ctx) => SignedInOr401(ctx)).DisableAntiforgery();

        // Status check from the page every minute. Does NOT renew the idle timeout (see WebAuthHandler),
        // so an idle tab notices when its sign-in has ended and goes to Sign in.
        app.MapGet("/auth/status", (HttpContext ctx) => SignedInOr401(ctx));
    }

    private static IResult SignedInOr401(HttpContext ctx) => ctx.User.Identity?.IsAuthenticated == true
        ? Results.NoContent()
        : Results.Json(new { reason = ctx.Items[SignedOutReasonItem] as string ?? "signedout" }, statusCode: StatusCodes.Status401Unauthorized);

    /// <summary>Requests that check the sign-in without counting as activity.</summary>
    internal static bool IsPassiveCheck(HttpRequest request) => request.Path.StartsWithSegments("/auth/status", StringComparison.OrdinalIgnoreCase);

    private static async Task<IResult> SignInAsync(HttpContext ctx, string userId, bool remember,
        IWebSignInService signIns, WebSignInPolicy policy, IServiceProvider services)
    {
        var (signIn, token) = await signIns.CreateAsync(userId, remember,
            ctx.Request.Headers.UserAgent.ToString(), ctx.Connection.RemoteIpAddress?.ToString(), policy, ctx.RequestAborted).ConfigureAwait(false);
        WriteCookie(ctx, token, signIn);

        // The user's row and personal workspace. Their preferences are NOT copied into the global
        // config any more (Phase 145 Part B): each conversation applies its owner's pick itself.
        _ = Task.Run(async () =>
        {
            try
            {
                await Program.SeedUserAndWorkspaceAsync(services, userId).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // best-effort; sign-in already succeeded
            catch (Exception ex)
            {
                services.GetService<ILoggerFactory>()?.CreateLogger("Sovrant.Web.Auth")
                    .LogWarning(ex, "Post-sign-in setup failed for {UserId}", userId);
            }
#pragma warning restore CA1031
        });
        // Phase 141: every sign-in lands on Home.
        return Results.Redirect("/dashboard");
    }

    internal static string LoginUrl(string? error = null, string? info = null, string? email = null, string? reason = null)
    {
        var q = new List<string>();
        if (error is not null) q.Add("error=" + Uri.EscapeDataString(error));
        if (info is not null) q.Add("info=" + Uri.EscapeDataString(info));
        if (reason is not null) q.Add("reason=" + Uri.EscapeDataString(reason));
        if (!string.IsNullOrWhiteSpace(email)) q.Add("email=" + Uri.EscapeDataString(email.Trim()));
        return q.Count == 0 ? "/login" : "/login?" + string.Join('&', q);
    }
}

public sealed class SignInForm
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Remember { get; set; }
    internal bool IsRemember => string.Equals(Remember, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(Remember, "true", StringComparison.OrdinalIgnoreCase);
}

public sealed class SignOutForm
{
    /// <summary>"all" signs out of every browser; anything else just this one.</summary>
    public string? Scope { get; set; }
}

/// <summary>Validates the sign-in cookie on every request (which also renews the idle timeout).</summary>
internal sealed class WebAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IWebSignInService signIns,
    WebSignInPolicy policy) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Cookies[WebAuth.CookieName];
        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        var check = await signIns.CheckAsync(token, policy, touch: !WebAuth.IsPassiveCheck(Request), Context.RequestAborted).ConfigureAwait(false);
        if (check.IsValid && check.SignIn is not null && check.Role is not null)
            return AuthenticateResult.Success(new AuthenticationTicket(WebAuth.ToPrincipal(check.SignIn, check.Role), WebAuth.Scheme));

        // Refused: remember why (for the Sign in notice) and drop the dead cookie.
        Context.Items[WebAuth.SignedOutReasonItem] = check.Status switch
        {
            WebSignInStatus.TimedOut => WebAuth.ReasonTimedOut,
            WebSignInStatus.Revoked when check.RevokedReason == WebSignInRevokeReasons.Admin => WebAuth.ReasonRevoked,
            _ => null,
        };
        WebAuth.DeleteCookie(Context);
        return AuthenticateResult.NoResult();
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Redirect(WebAuth.LoginUrl(reason: Context.Items[WebAuth.SignedOutReasonItem] as string));
        return Task.CompletedTask;
    }
}
