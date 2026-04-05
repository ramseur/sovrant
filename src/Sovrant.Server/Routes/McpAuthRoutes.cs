using Sovrant.Runtime.Mcp;

namespace Sovrant.Server.Routes;

/// <summary>Registers the <c>GET /v1/mcp/auth/callback</c> OAuth callback endpoint.</summary>
internal static class McpAuthRoutes
{
    public static void Map(WebApplication app)
    {
        // This endpoint is intentionally excluded from bearer-token auth — the OAuth provider
        // redirects here unauthenticated. The CSRF state parameter is the security control.
        app.MapGet("/v1/mcp/auth/callback", HandleAsync)
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        string? code,
        string? state,
        McpOAuthService oauthService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Results.Content(
                ErrorPage("Missing <code>code</code> or <code>state</code> query parameter."),
                "text/html",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var serverName = await oauthService.ExchangeCodeAsync(state, code, ct)
                .ConfigureAwait(false);

            return Results.Content(SuccessPage(serverName), "text/html");
        }
        catch (InvalidOperationException ex)
        {
            return Results.Content(
                ErrorPage(HtmlEncode(ex.Message)),
                "text/html",
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException ex)
        {
            return Results.Content(
                ErrorPage($"Token endpoint error: {HtmlEncode(ex.Message ?? string.Empty)}"),
                "text/html",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static string SuccessPage(string serverName) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Authorization Successful — Sovrant</title>
          <style>
            body { font-family: system-ui, sans-serif; max-width: 480px; margin: 80px auto; padding: 0 24px; }
            h1 { color: #16a34a; }
          </style>
        </head>
        <body>
          <h1>&#10003; Authorization successful</h1>
          <p>Access to <strong>{{HtmlEncode(serverName)}}</strong> has been granted.</p>
          <p>You may close this tab and return to Sovrant.</p>
        </body>
        </html>
        """;

    private static string ErrorPage(string message) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Authorization Failed — Sovrant</title>
          <style>
            body { font-family: system-ui, sans-serif; max-width: 480px; margin: 80px auto; padding: 0 24px; }
            h1 { color: #dc2626; }
          </style>
        </head>
        <body>
          <h1>&#10007; Authorization failed</h1>
          <p>{{message}}</p>
        </body>
        </html>
        """;

    /// <summary>Minimal HTML encoder — prevents XSS in user-controlled values rendered in pages.</summary>
    private static string HtmlEncode(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
}
