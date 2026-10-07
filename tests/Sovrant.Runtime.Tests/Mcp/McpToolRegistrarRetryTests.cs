using System.IO.Pipelines;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Tools;
using Xunit;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>
/// Phase 139 — a server that's unreachable at startup reconnects on its own and its tools appear;
/// credential errors are never retried. Uses a real in-process MCP server over pipes.
/// </summary>
public sealed class McpToolRegistrarRetryTests : IAsyncDisposable
{
    private static readonly McpServerConfig Http = new() { Url = new Uri("https://api.pixellab.ai/mcp") };
    private static readonly TimeSpan[] FastRetries = [TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40)];

    private readonly InMemoryToolRegistry _tools = new();
    private readonly McpClientRegistry _clients = new();
    private readonly McpServerStatusRegistry _status = new();
    private readonly List<IAsyncDisposable> _servers = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var s in _servers) await s.DisposeAsync();
    }

    [Fact]
    public async Task Unreachable_At_Startup_Reconnects_On_Its_Own_And_Registers_Tools()
    {
        var factory = new ScriptedFactory(this, failures: 2, Dns);
        await using var registrar = Build(factory);

        await registrar.RegisterAllAsync(new Dictionary<string, McpServerConfig> { ["pixellab"] = Http });

        var first = _status.Get("pixellab")!;
        Assert.Equal(McpServerState.Unavailable, first.State);
        Assert.Equal(McpFailureKind.Dns, first.Failure!.Kind);
        Assert.NotNull(first.NextRetryAt);
        Assert.False(_tools.TryGetHandler("create_character", out _));

        await WaitUntil(() => _status.Get("pixellab")?.State == McpServerState.Connected);

        Assert.True(_tools.TryGetHandler("create_character", out _));
        Assert.Equal("pixellab", _clients.ToolToServer["create_character"]);
        Assert.Equal(3, factory.Calls);
    }

    [Fact]
    public async Task Credential_Errors_Are_Never_Retried()
    {
        var factory = new ScriptedFactory(this, failures: int.MaxValue,
            () => new HttpRequestException("denied", null, System.Net.HttpStatusCode.Unauthorized));
        await using var registrar = Build(factory);

        await registrar.RegisterAllAsync(new Dictionary<string, McpServerConfig> { ["pixellab"] = Http });
        await Task.Delay(300);

        var status = _status.Get("pixellab")!;
        Assert.Equal(McpFailureKind.Credentials, status.Failure!.Kind);
        Assert.Null(status.NextRetryAt);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task Retries_Stop_After_The_Last_Delay_And_Retry_Now_Still_Works()
    {
        var factory = new ScriptedFactory(this, failures: 4, Dns);
        await using var registrar = Build(factory);

        await registrar.RegisterAllAsync(new Dictionary<string, McpServerConfig> { ["pixellab"] = Http });
        // Wait for the end state (no retry scheduled) rather than a fixed pause: under load the last
        // retry's status update can land later than 200 ms.
        await WaitUntil(() => factory.Calls == 4 && _status.Get("pixellab") is { State: McpServerState.Unavailable, NextRetryAt: null });

        var exhausted = _status.Get("pixellab")!;
        Assert.Null(exhausted.NextRetryAt);
        Assert.Equal(4, factory.Calls); // startup + 3 retries
        Assert.Contains("use Retry in Integrations", exhausted.Message, StringComparison.Ordinal);

        var after = await registrar.RetryAsync("pixellab");
        Assert.Equal(McpServerState.Connected, after.State);
    }

    [Fact]
    public async Task A_Server_Added_From_Integrations_Also_Retries_Network_Failures()
    {
        var factory = new ScriptedFactory(this, failures: 1, Dns);
        await using var registrar = Build(factory);

        await Assert.ThrowsAsync<HttpRequestException>(() => registrar.ReconnectServerAsync("pixellab", Http));
        Assert.NotNull(_status.Get("pixellab")!.NextRetryAt);

        await WaitUntil(() => _status.Get("pixellab")?.State == McpServerState.Connected);
        Assert.True(_tools.TryGetHandler("create_character", out _));
    }

    [Fact]
    public async Task One_Unreachable_Server_Does_Not_Stop_The_Others()
    {
        var factory = new ScriptedFactory(this, failures: int.MaxValue, Dns, failOnly: "pixellab");
        await using var registrar = Build(factory);

        await registrar.RegisterAllAsync(new Dictionary<string, McpServerConfig> { ["pixellab"] = Http, ["github"] = Http });

        Assert.Equal(McpServerState.Connected, _status.Get("github")!.State);
        // pixellab's background retries (40 ms apart here) pass through a connecting state; wait for
        // it to settle rather than catching it mid-retry under load.
        await WaitUntil(() => _status.Get("pixellab") is { State: McpServerState.Unavailable, NextRetryAt: null });
        Assert.Equal(McpServerState.Connected, _status.Get("github")!.State);
    }

    [Fact]
    public async Task Forgetting_A_Server_Cancels_Its_Retries()
    {
        var factory = new ScriptedFactory(this, failures: int.MaxValue, Dns);
        await using var registrar = Build(factory);

        await registrar.RegisterAllAsync(new Dictionary<string, McpServerConfig> { ["pixellab"] = Http });
        registrar.ForgetServer("pixellab");
        await Task.Delay(250);

        Assert.Null(_status.Get("pixellab"));
        Assert.Equal(1, factory.Calls);
    }

    private static Exception Dns() =>
        new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known.", new SocketException((int)SocketError.HostNotFound));

    private McpToolRegistrar Build(IMcpClientFactory factory) =>
        new(factory, _tools, _clients, _status, NullLogger<McpToolRegistrar>.Instance) { RetryDelays = FastRetries };

    private static async Task WaitUntil(Func<bool> condition)
    {
        // Up to 15 s: a busy test run (the whole suite in parallel) can slow the retries well past 3 s.
        for (var i = 0; i < 500 && !condition(); i++)
            await Task.Delay(30);
        Assert.True(condition(), "condition not reached in time");
    }

    /// <summary>Connects to a real in-process MCP server exposing one tool.</summary>
    private async Task<McpClient> ConnectInProcessAsync(CancellationToken ct)
    {
        var toServer = new Pipe();
        var toClient = new Pipe();
        var options = new McpServerOptions
        {
            ToolCollection = [McpServerTool.Create(() => "ok", new McpServerToolCreateOptions { Name = "create_character" })],
        };
        var server = McpServer.Create(new StreamServerTransport(toServer.Reader.AsStream(), toClient.Writer.AsStream()), options);
        _servers.Add(server);
        _ = server.RunAsync(CancellationToken.None);
        var client = await McpClient.CreateAsync(new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()), cancellationToken: ct);
        return client;
    }

    private sealed class ScriptedFactory(McpToolRegistrarRetryTests owner, int failures, Func<Exception> error, string? failOnly = null) : IMcpClientFactory
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public Task<McpClient> CreateAsync(string name, McpServerConfig config, CancellationToken ct = default)
        {
            var failing = failOnly is null || failOnly == name;
            var n = failing ? Interlocked.Increment(ref _calls) : 0;
            if (failing && n <= failures)
                return Task.FromException<McpClient>(error());
            return owner.ConnectInProcessAsync(ct);
        }
    }
}
