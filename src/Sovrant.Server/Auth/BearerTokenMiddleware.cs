using System.Security.Cryptography;
using System.Text;
using Sovrant.Runtime.Auth;

namespace Sovrant.Server.Auth;

/// <summary>
/// Authenticates incoming requests via a bearer token. Phase 38 introduces
/// per-user tokens alongside the legacy static <c>SOVRANT_TOKEN</c>:
///
/// <list type="bullet">
///   <item>If the bearer value starts with the per-user prefix
///         (<see cref="ITokenService.TokenPrefix"/>, currently
///         <c>svt_</c>) it is resolved through <see cref="ITokenService"/>.
///         A successful resolve attaches <see cref="SovrantHttpContextKeys.UserId"/>,
///         <see cref="SovrantHttpContextKeys.TokenId"/>, and
///         <see cref="SovrantHttpContextKeys.AuthMode"/> to <see cref="HttpContext.Items"/>
///         so downstream routes can scope their data by user.</item>
///   <item>Otherwise the value is compared in constant time against the
///         legacy static token from <c>SOVRANT_TOKEN</c> /
///         <c>Server:Token</c>. On match the request is authenticated as
///         <see cref="SovrantHttpContextKeys.AuthModeStatic"/> with no user
///         identity attached — preserving the pre-Phase-38 behaviour for
///         CI scripts and the local CLI.</item>
/// </list>
///
/// <para><c>/health</c> and <c>OPTIONS</c> preflight remain unauthenticated
/// so load balancers and browser CORS keep working.</para>
/// </summary>
internal sealed class BearerTokenMiddleware : IMiddleware
{
    private readonly byte[] _expectedStaticTokenBytes;
    private readonly bool _hasStaticToken;

    public BearerTokenMiddleware(IConfiguration configuration)
    {
        var staticToken =
            Environment.GetEnvironmentVariable("SOVRANT_TOKEN")
            ?? configuration["Server:Token"]
            ?? string.Empty;

        _hasStaticToken = !string.IsNullOrEmpty(staticToken);
        _expectedStaticTokenBytes = _hasStaticToken
            ? Encoding.UTF8.GetBytes(staticToken)
            : Array.Empty<byte>();
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Allow health-check and OPTIONS preflight through unauthenticated.
        if (context.Request.Method == HttpMethods.Options ||
            context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!TryExtractToken(context, out var presented))
        {
            await Reject(context).ConfigureAwait(false);
            return;
        }

        // ── Per-user token path (svt_*) ──────────────────────────────────
        if (presented.StartsWith(ITokenService.TokenPrefix, StringComparison.Ordinal))
        {
            var tokenService = context.RequestServices.GetService<ITokenService>();
            if (tokenService is null)
            {
                // ITokenService is wired by AddSovrantRuntime; if it's missing
                // the deployment is misconfigured. Fail closed rather than
                // silently downgrading to the static path.
                await Reject(context).ConfigureAwait(false);
                return;
            }

            var resolved = await tokenService.ResolveAsync(presented, context.RequestAborted).ConfigureAwait(false);
            if (resolved is null)
            {
                await Reject(context).ConfigureAwait(false);
                return;
            }

            context.Items[SovrantHttpContextKeys.UserId] = resolved.UserId;
            context.Items[SovrantHttpContextKeys.TokenId] = resolved.TokenId;
            context.Items[SovrantHttpContextKeys.AuthMode] = SovrantHttpContextKeys.AuthModeToken;

            await next(context).ConfigureAwait(false);
            return;
        }

        // ── Legacy static token path ─────────────────────────────────────
        if (!_hasStaticToken)
        {
            await Reject(context).ConfigureAwait(false);
            return;
        }

        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        if (!CryptographicOperations.FixedTimeEquals(presentedBytes, _expectedStaticTokenBytes))
        {
            await Reject(context).ConfigureAwait(false);
            return;
        }

        context.Items[SovrantHttpContextKeys.AuthMode] = SovrantHttpContextKeys.AuthModeStatic;
        await next(context).ConfigureAwait(false);
    }

    private static async Task Reject(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" }).ConfigureAwait(false);
    }

    private static bool TryExtractToken(HttpContext context, out string token)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();
        if (header is not null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = header["Bearer ".Length..].Trim();
            return token.Length > 0;
        }

        token = string.Empty;
        return false;
    }
}
