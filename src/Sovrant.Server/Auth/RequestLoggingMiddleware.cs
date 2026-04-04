using System.Diagnostics;

namespace Sovrant.Server.Auth;

/// <summary>
/// Logs the start and completion of every HTTP request with method, path, status, and duration.
/// </summary>
internal sealed partial class RequestLoggingMiddleware : IMiddleware
{
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Request started: {Method} {Path}")]
    private static partial void LogRequestStarted(ILogger logger, string method, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request complete: {Method} {Path} → {StatusCode} in {DurationMs}ms")]
    private static partial void LogRequestComplete(ILogger logger, string method, string path, int statusCode, long durationMs);

    public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        LogRequestStarted(_logger, method, path);
        var sw = Stopwatch.StartNew();

        await next(context).ConfigureAwait(false);

        sw.Stop();
        LogRequestComplete(_logger, method, path, context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
}
