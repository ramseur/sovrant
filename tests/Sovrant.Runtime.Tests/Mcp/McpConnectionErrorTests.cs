using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Xunit;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>Phase 139 — each MCP connection failure maps to one friendly sentence.</summary>
public sealed class McpConnectionErrorTests
{
    private static readonly McpServerConfig Http = new() { Url = new Uri("https://api.pixellab.ai/mcp") };

    [Fact]
    public void Dns_Failure_Names_The_Host_And_Retries()
    {
        var ex = new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known. (api.pixellab.ai:443)",
            new SocketException((int)SocketError.HostNotFound));

        var f = McpConnectionError.Classify("pixellab", Http, ex);

        Assert.Equal(McpFailureKind.Dns, f.Kind);
        Assert.True(f.ShouldRetry);
        Assert.Equal("Couldn't reach api.pixellab.ai", f.Reason);
        Assert.Equal("Check your internet connection.", f.Advice);
    }

    [Fact]
    public void Dns_Failure_Is_Recognised_From_The_Message_Alone()
    {
        var f = McpConnectionError.Classify("pixellab", Http, new InvalidOperationException("No such host is known."));
        Assert.Equal(McpFailureKind.Dns, f.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void Rejected_Credentials_Are_Never_Retried(HttpStatusCode code)
    {
        var f = McpConnectionError.Classify("pixellab", Http, new HttpRequestException("denied", null, code));

        Assert.Equal(McpFailureKind.Credentials, f.Kind);
        Assert.False(f.ShouldRetry);
        Assert.Equal("pixellab rejected the credentials", f.Reason);
        Assert.Equal("check key", f.ShortLabel);
    }

    [Fact]
    public void Status_401_In_The_Message_Counts_As_Credentials()
    {
        var f = McpConnectionError.Classify("pixellab", Http,
            new InvalidOperationException("Response status code does not indicate success: 401 (Unauthorized)."));
        Assert.Equal(McpFailureKind.Credentials, f.Kind);
    }

    [Fact]
    public void Timeout_Is_Unreachable_And_Retried()
    {
        var f = McpConnectionError.Classify("pixellab", Http, new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));
        Assert.Equal(McpFailureKind.Unreachable, f.Kind);
        Assert.Equal("pixellab isn't responding (timed out)", f.Reason);
        Assert.True(f.ShouldRetry);
    }

    [Fact]
    public void Refused_Connection_Is_Unreachable()
    {
        var f = McpConnectionError.Classify("local", Http,
            new HttpRequestException(HttpRequestError.ConnectionError, "refused", new SocketException((int)SocketError.ConnectionRefused)));
        Assert.Equal(McpFailureKind.Unreachable, f.Kind);
        Assert.Contains("connection refused", f.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Certificate_Problem_Is_Tls()
    {
        var f = McpConnectionError.Classify("pixellab", Http, new HttpRequestException("ssl", new AuthenticationException("remote certificate is invalid")));
        Assert.Equal(McpFailureKind.Tls, f.Kind);
        Assert.Equal("Secure connection to pixellab failed (certificate problem)", f.Reason);
    }

    [Fact]
    public void Anything_Else_Is_Other_With_A_Short_Message_And_No_Retry()
    {
        var f = McpConnectionError.Classify("files", new McpServerConfig { Command = "npx" },
            new InvalidOperationException("Server process exited unexpectedly.\n   at Something.Stack.Trace()"));

        Assert.Equal(McpFailureKind.Other, f.Kind);
        Assert.False(f.ShouldRetry);
        Assert.Equal("Couldn't connect to files: Server process exited unexpectedly", f.Reason);
        Assert.Contains("Stack.Trace", f.Detail, StringComparison.Ordinal); // detail keeps everything, for the log only
    }

    [Fact]
    public void Status_Message_Ends_With_What_Happens_Next()
    {
        var dns = McpConnectionError.Classify("pixellab", Http, new InvalidOperationException("No such host is known."));
        var now = DateTimeOffset.UtcNow;

        var retrying = new McpServerStatus("pixellab", McpServerState.Unavailable, dns, NextRetryAt: now.AddSeconds(52), Attempt: 1, MaxAttempts: 3);
        Assert.Equal("Couldn't reach api.pixellab.ai. Check your internet connection. Retrying automatically.", retrying.Message);
        Assert.Equal("Next attempt in 52 s · retry 2 of 3", retrying.RetryNote(now));

        var exhausted = retrying with { NextRetryAt = null, Attempt = 3 };
        Assert.EndsWith("Automatic retries stopped; use Retry in Integrations.", exhausted.Message, StringComparison.Ordinal);

        var key = McpConnectionError.Classify("pixellab", Http, new HttpRequestException("x", null, HttpStatusCode.Unauthorized));
        var auth = new McpServerStatus("pixellab", McpServerState.Unavailable, key);
        Assert.Equal("pixellab rejected the credentials. Update the key in Integrations.", auth.Message);
        Assert.StartsWith("Not retried automatically", auth.RetryNote(now), StringComparison.Ordinal);
    }
}
