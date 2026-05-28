using System.Net;
using System.Text;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// Ephemeral loopback HTTP listener that catches the OAuth 2.0 authorization code redirect
/// for Desktop / CLI flows where no persistent HTTP server is running.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// await using var listener = McpOAuthCallbackListener.Start();
/// var authUrl = await oauthService.GenerateAuthorizationUrlAsync(name, listener.RedirectUri, ct);
/// Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
/// var (code, state) = await listener.WaitForCallbackAsync(ct);
/// await oauthService.ExchangeCodeAsync(state, code, ct);
/// </code>
/// </remarks>
public sealed class McpOAuthCallbackListener : IAsyncDisposable
{
    private readonly HttpListener _listener;

    /// <summary>The loopback redirect URI to pass to the OAuth provider.</summary>
    public Uri RedirectUri { get; }

    private McpOAuthCallbackListener(HttpListener listener, int port)
    {
        _listener = listener;
        RedirectUri = new Uri($"http://127.0.0.1:{port}/mcp-oauth-callback/");
    }

    /// <summary>Binds a random available loopback port and starts the listener.</summary>
    public static McpOAuthCallbackListener Start()
    {
        var port = FindFreePort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/mcp-oauth-callback/");
        listener.Start();
        return new McpOAuthCallbackListener(listener, port);
    }

    /// <summary>
    /// Waits for the OAuth provider to redirect back with a <c>code</c> and <c>state</c>.
    /// Sends a self-closing HTML page to the browser and returns the parameters.
    /// </summary>
    public async Task<(string Code, string State)> WaitForCallbackAsync(CancellationToken ct = default)
    {
        HttpListenerContext context;
        using (ct.Register(() => _listener.Abort()))
        {
            context = await Task.Run(_listener.GetContext, ct).ConfigureAwait(false);
        }

        var query = context.Request.QueryString;
        var error = query["error"];
        var code = query["code"];
        var state = query["state"];

        var html = string.IsNullOrEmpty(error) ? SuccessHtml() : ErrorHtml(error);
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
        context.Response.Close();

        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException($"OAuth provider returned error: {error}");

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            throw new InvalidOperationException("OAuth callback is missing required parameters.");

        return (code, state);
    }

    public ValueTask DisposeAsync()
    {
        try { _listener.Close(); }
        catch (ObjectDisposedException) { /* already stopped */ }
        return ValueTask.CompletedTask;
    }

    private static int FindFreePort()
    {
        using var sock = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        sock.Start();
        var port = ((IPEndPoint)sock.LocalEndpoint).Port;
        sock.Stop();
        return port;
    }

    private static string SuccessHtml() => """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Authorization Successful — Sovrant</title>
          <style>body{font-family:system-ui,sans-serif;max-width:480px;margin:80px auto;padding:0 24px;}h1{color:#16a34a;}</style>
        </head>
        <body>
          <h1>&#10003; Authorization successful</h1>
          <p>You may close this tab and return to Sovrant.</p>
          <script>setTimeout(()=>window.close(),1500);</script>
        </body>
        </html>
        """;

    private static string ErrorHtml(string error)
    {
        var msg = HtmlEncode(error);
        return """
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <title>Authorization Failed</title>
              <style>body{font-family:system-ui,sans-serif;max-width:480px;margin:80px auto;padding:0 24px;}h1{color:#dc2626;}</style>
            </head>
            <body>
              <h1>&#10007; Authorization failed</h1>
              <p>
            """ + msg + """
              </p>
            </body>
            </html>
            """;
    }

    private static string HtmlEncode(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
}
