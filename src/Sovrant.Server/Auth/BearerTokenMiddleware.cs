using Sovrant.Runtime.Auth;

namespace Sovrant.Server.Auth;

/// <summary>
/// Phase 85 — Authenticates requests via per-user <c>svt_</c> bearer tokens.
/// The legacy static <c>SOVRANT_TOKEN</c> path has been removed.
///
/// <para>Unauthenticated paths: <c>/health</c>, <c>OPTIONS</c>, and the
/// auth endpoints (<c>/v1/auth/login</c>, <c>/v1/auth/register</c>,
/// <c>/v1/auth/use-reset-token</c>, <c>/v1/auth/registration/status</c>).</para>
/// </summary>
internal sealed class BearerTokenMiddleware : IMiddleware
{
    private static readonly HashSet<string> UnauthenticatedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/v1/auth/login",
        "/v1/auth/register",
        "/v1/auth/use-reset-token",
        "/v1/auth/registration/status",
    };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Method == HttpMethods.Options ||
            context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
            UnauthenticatedPaths.Contains(context.Request.Path.Value ?? string.Empty))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!TryExtractToken(context, out var presented))
        {
            await Reject(context).ConfigureAwait(false);
            return;
        }

        if (!presented.StartsWith(ITokenService.TokenPrefix, StringComparison.Ordinal))
        {
            await Reject(context).ConfigureAwait(false);
            return;
        }

        var tokenService = context.RequestServices.GetRequiredService<ITokenService>();
        var resolved = await tokenService.ResolveAsync(presented, context.RequestAborted).ConfigureAwait(false);
        if (resolved is null)
        {
            await Reject(context).ConfigureAwait(false);
            return;
        }

        context.Items[SovrantHttpContextKeys.UserId] = resolved.Token.UserId;
        context.Items[SovrantHttpContextKeys.TokenId] = resolved.Token.TokenId;
        context.Items[SovrantHttpContextKeys.Role] = resolved.Role;
        context.Items[SovrantHttpContextKeys.AuthMode] = SovrantHttpContextKeys.AuthModeToken;

        await next(context).ConfigureAwait(false);
    }

    private static Task Reject(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
    }

    private static bool TryExtractToken(HttpContext context, out string token)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();
        if (header is not null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = header["Bearer ".Length..].Trim();
            return token.Length > 0;
        }

        // SignalR WebSocket transport sends the token via query string (Phase 61).
        if (context.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase) &&
            context.Request.Query.TryGetValue("access_token", out var queryToken) &&
            queryToken.Count > 0)
        {
            token = queryToken[0]?.Trim() ?? string.Empty;
            return token.Length > 0;
        }

        token = string.Empty;
        return false;
    }
}
