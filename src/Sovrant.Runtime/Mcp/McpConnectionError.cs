using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using Sovrant.Runtime.Config;

namespace Sovrant.Runtime.Mcp;

/// <summary>Why an MCP server couldn't be connected (Phase 139).</summary>
public enum McpFailureKind
{
    /// <summary>The host name couldn't be resolved (no internet, DNS hiccup, typo).</summary>
    Dns,

    /// <summary>The server refused the connection, timed out, or dropped it.</summary>
    Unreachable,

    /// <summary>The server answered 401/403: the key or token is wrong or expired.</summary>
    Credentials,

    /// <summary>The secure (TLS) connection failed, e.g. a certificate problem.</summary>
    Tls,

    /// <summary>Anything else (a stdio server that won't start, a protocol error, …).</summary>
    Other,
}

/// <summary>
/// A classified MCP connection failure: a plain-language reason and what to do, for the UI and
/// the one-line log entry. <see cref="Detail"/> (the full exception) is for the log file only.
/// </summary>
public sealed record McpConnectionFailure(McpFailureKind Kind, string Reason, string? Advice, bool ShouldRetry, string Detail)
{
    /// <summary>Short label for list rows: "retrying", "check key", ….</summary>
    public string ShortLabel => Kind switch
    {
        McpFailureKind.Credentials => "check key",
        _ when ShouldRetry => "retrying",
        _ => "not connected",
    };
}

/// <summary>
/// Phase 139 — turns an MCP connection exception into one friendly sentence. Network-type failures
/// (DNS, refused/timeout, TLS) are worth retrying; credential errors aren't, since retrying can't fix a key.
/// </summary>
public static class McpConnectionError
{
    private const int MaxDetailLength = 160;

    public static McpConnectionFailure Classify(string serverName, McpServerConfig? config, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var host = config?.Url?.Host;
        var detail = exception.ToString();

        foreach (var ex in Chain(exception))
        {
            switch (ex)
            {
                case HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden }:
                    return Credentials(serverName, detail);
                case HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError }:
                    return Dns(serverName, host, detail);
                case HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError }:
                case AuthenticationException:
                    return Tls(serverName, detail);
                case SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain }:
                    return Dns(serverName, host, detail);
                case SocketException { SocketErrorCode: SocketError.ConnectionRefused }:
                    return Unreachable(serverName, "connection refused", detail);
                case SocketException { SocketErrorCode: SocketError.TimedOut }:
                case TimeoutException:
                case TaskCanceledException:
                    return Unreachable(serverName, "timed out", detail);
                case SocketException { SocketErrorCode: SocketError.NetworkUnreachable or SocketError.HostUnreachable or SocketError.NetworkDown }:
                    return Unreachable(serverName, "network unreachable", detail);
                case SocketException { SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted }:
                    return Unreachable(serverName, "connection lost", detail);
                case HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError }:
                    return Unreachable(serverName, "connection refused", detail);
            }
        }

        // Some transports only put the HTTP status in the message text.
        foreach (var ex in Chain(exception))
        {
            var m = ex.Message;
            if (m.Contains("401", StringComparison.Ordinal) || m.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
                || m.Contains("403", StringComparison.Ordinal) || m.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
                return Credentials(serverName, detail);
            if (m.Contains("No such host is known", StringComparison.OrdinalIgnoreCase)
                || m.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase))
                return Dns(serverName, host, detail);
        }

        return new McpConnectionFailure(McpFailureKind.Other, $"Couldn't connect to {serverName}: {Short(Innermost(exception).Message)}",
            null, ShouldRetry: false, detail);
    }

    private static McpConnectionFailure Dns(string name, string? host, string detail) =>
        new(McpFailureKind.Dns, $"Couldn't reach {host ?? name}", "Check your internet connection.", ShouldRetry: true, detail);

    private static McpConnectionFailure Unreachable(string name, string why, string detail) =>
        new(McpFailureKind.Unreachable, $"{name} isn't responding ({why})", null, ShouldRetry: true, detail);

    private static McpConnectionFailure Credentials(string name, string detail) =>
        new(McpFailureKind.Credentials, $"{name} rejected the credentials", "Update the key in Integrations.", ShouldRetry: false, detail);

    private static McpConnectionFailure Tls(string name, string detail) =>
        new(McpFailureKind.Tls, $"Secure connection to {name} failed (certificate problem)", null, ShouldRetry: true, detail);

    private static IEnumerable<Exception> Chain(Exception root)
    {
        var stack = new Stack<Exception>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var ex = stack.Pop();
            yield return ex;
            if (ex is AggregateException agg)
                foreach (var inner in agg.InnerExceptions) stack.Push(inner);
            else if (ex.InnerException is not null)
                stack.Push(ex.InnerException);
        }
    }

    private static Exception Innermost(Exception ex)
    {
        while (ex.InnerException is not null) ex = ex.InnerException;
        return ex;
    }

    private static string Short(string message)
    {
        var line = message.Split('\n', 2)[0].Trim().TrimEnd('.');
        return line.Length <= MaxDetailLength ? line : line[..MaxDetailLength].TrimEnd() + "…";
    }
}
