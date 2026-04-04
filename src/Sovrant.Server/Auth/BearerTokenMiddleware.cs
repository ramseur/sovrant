namespace Sovrant.Server.Auth;

/// <summary>
/// Middleware that enforces a static bearer token on every request.
/// The expected token is read from the <c>SOVRANT_TOKEN</c> environment variable.
/// Returns 401 if the header is absent or does not match.
/// </summary>
internal sealed class BearerTokenMiddleware : IMiddleware
{
    private readonly string _expectedToken;

    public BearerTokenMiddleware(IConfiguration configuration)
    {
        _expectedToken =
            Environment.GetEnvironmentVariable("SOVRANT_TOKEN")
            ?? configuration["Server:Token"]
            ?? string.Empty;
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

        if (!TryExtractToken(context, out var token) ||
            !string.Equals(token, _expectedToken, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(_expectedToken))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" }).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool TryExtractToken(HttpContext context, out string token)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();
        if (header is not null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = header["Bearer ".Length..].Trim();
            return true;
        }

        token = string.Empty;
        return false;
    }
}
