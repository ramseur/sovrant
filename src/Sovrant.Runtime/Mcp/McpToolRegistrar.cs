using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sovrant.Api.Types;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Mcp;

/// <summary>Discovers tools from MCP servers and registers them in the tool registry.</summary>
/// <remarks>
/// Phase 139: every connection attempt records the server's status in
/// <see cref="McpServerStatusRegistry"/>. A failure is logged as one friendly line (the full
/// exception goes to the log at debug level), and network-type failures are retried in the
/// background with backoff, so a server that was unreachable at startup comes back on its own.
/// </remarks>
public sealed partial class McpToolRegistrar : IAsyncDisposable
{
    private static readonly TimeSpan[] DefaultRetryDelays =
        [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    private readonly IMcpClientFactory _clientFactory;
    private readonly IToolRegistry _registry;
    private readonly McpClientRegistry _clientRegistry;
    private readonly McpServerStatusRegistry _status;
    private readonly ILogger<McpToolRegistrar> _logger;
    private readonly List<McpClient> _clients = [];
    private readonly SemaphoreSlim _clientsLock = new(1, 1);
    private readonly ConcurrentDictionary<string, McpServerConfig> _configs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingRetries = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();

    [LoggerMessage(Level = LogLevel.Information, Message = "Registering {Count} tools from MCP server '{ServerName}'")]
    private static partial void LogRegistering(ILogger logger, int count, string serverName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MCP server '{ServerName}' unavailable: {Message}")]
    private static partial void LogUnavailable(ILogger logger, string serverName, string message);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MCP server '{ServerName}' connection failure detail")]
    private static partial void LogFailureDetail(ILogger logger, string serverName, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP server '{ServerName}' reconnected")]
    private static partial void LogReconnected(ILogger logger, string serverName);

    public McpToolRegistrar(
        IMcpClientFactory clientFactory,
        IToolRegistry registry,
        McpClientRegistry clientRegistry,
        McpServerStatusRegistry status,
        ILogger<McpToolRegistrar> logger)
    {
        _clientFactory = clientFactory;
        _registry = registry;
        _clientRegistry = clientRegistry;
        _status = status;
        _logger = logger;
    }

    /// <summary>Waits before each automatic retry of a network-type failure (≈10 s, 1 min, 5 min). Settable for tests.</summary>
    public IReadOnlyList<TimeSpan> RetryDelays { get; set; } = DefaultRetryDelays;

    /// <summary>
    /// Connects to each configured MCP server, lists its tools, and registers them
    /// in the tool registry with handlers that forward calls back to the server.
    /// A server that can't be reached never blocks or fails startup.
    /// </summary>
    public async Task RegisterAllAsync(
        IReadOnlyDictionary<string, McpServerConfig> servers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(servers);

        foreach (var (name, config) in servers)
        {
            _configs[name] = config;
            await TryConnectAsync(name, config, attempt: 0, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retry now (the Integrations "Retry" button): cancels any pending automatic retry and connects
    /// immediately. Never throws for connection failures; the result is the server's new status.
    /// </summary>
    public async Task<McpServerStatus> RetryAsync(string serverName, McpServerConfig? config = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        config ??= _configs.TryGetValue(serverName, out var known) ? known : null;
        if (config is null)
            throw new InvalidOperationException($"No configuration is known for MCP server '{serverName}'.");
        _configs[serverName] = config;
        CancelPendingRetry(serverName);
        await TryConnectAsync(serverName, config, attempt: 0, ct).ConfigureAwait(false);
        return _status.Get(serverName)!;
    }

    /// <summary>Stops retrying a server and forgets its status (the server was removed).</summary>
    public void ForgetServer(string serverName)
    {
        CancelPendingRetry(serverName);
        _configs.TryRemove(serverName, out _);
        _status.Remove(serverName);
    }

    private async Task<bool> TryConnectAsync(string name, McpServerConfig config, int attempt, CancellationToken ct)
    {
        var previous = _status.Get(name);
        _status.Set(new McpServerStatus(name, McpServerState.Connecting, previous?.Failure, Attempt: attempt, MaxAttempts: RetryDelays.Count));
        try
        {
            await ConnectCoreAsync(name, config, ct).ConfigureAwait(false);
            _status.Set(new McpServerStatus(name, McpServerState.Connected));
            if (previous?.IsUnavailable == true)
                LogReconnected(_logger, name);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // a single bad MCP must not crash startup or the retry loop
        catch (Exception ex)
#pragma warning restore CA1031
        {
            var failure = McpConnectionError.Classify(name, config, ex);
            var retry = failure.ShouldRetry && attempt < RetryDelays.Count;
            var status = new McpServerStatus(name, McpServerState.Unavailable, failure,
                NextRetryAt: retry ? DateTimeOffset.UtcNow + RetryDelays[attempt] : null,
                Attempt: attempt, MaxAttempts: RetryDelays.Count);
            _status.Set(status);
            LogUnavailable(_logger, name, status.Message);
            LogFailureDetail(_logger, name, ex);
            if (retry)
                ScheduleRetry(name, RetryDelays[attempt], attempt + 1);
            return false;
        }
    }

    private void ScheduleRetry(string name, TimeSpan delay, int attempt)
    {
        CancelPendingRetry(name);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _pendingRetries[name] = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                _pendingRetries.TryRemove(new KeyValuePair<string, CancellationTokenSource>(name, cts));
                if (_configs.TryGetValue(name, out var config))
                    await TryConnectAsync(name, config, attempt, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Superseded by Retry, the server was removed, or the app is shutting down.
            }
            finally
            {
                cts.Dispose();
            }
        });
    }

    // The retry task owns its CancellationTokenSource and disposes it in its finally block;
    // here we only signal it.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Disposed by the scheduled retry task that created it.")]
    private void CancelPendingRetry(string name)
    {
        if (_pendingRetries.TryRemove(name, out var cts))
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { /* the retry already finished */ }
        }
    }

    /// <summary>Replaces any existing client for the server, then connects and registers its tools.</summary>
    private async Task ConnectCoreAsync(string serverName, McpServerConfig config, CancellationToken ct)
    {
        await _clientsLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_clientRegistry.Clients.TryGetValue(serverName, out var existing))
            {
                _clients.Remove(existing);
                _clientRegistry.Unregister(serverName);
                await existing.DisposeAsync().ConfigureAwait(false);
            }
            _clientRegistry.ForgetServerTools(serverName);

            var client = await _clientFactory.CreateAsync(serverName, config, ct).ConfigureAwait(false);
            _clients.Add(client);
            _clientRegistry.Register(serverName, client);
            await RegisterFromClientAsync(serverName, client, ct).ConfigureAwait(false);
        }
        finally
        {
            _clientsLock.Release();
        }
    }

    private async Task RegisterFromClientAsync(string serverName, McpClient client, CancellationToken ct)
    {
        var tools = await client.ListToolsAsync(cancellationToken: ct).ConfigureAwait(false);
        LogRegistering(_logger, tools.Count, serverName);

        foreach (var tool in tools)
        {
            var capturedClient = client;
            var capturedName = tool.Name;

            JsonElement inputSchema;
            if (tool.JsonSchema is JsonElement schemaElem)
                inputSchema = schemaElem;
            else
                inputSchema = JsonDocument.Parse("{}").RootElement;

            var definition = new ToolDefinition(tool.Name, inputSchema) { Description = tool.Description };
            _clientRegistry.MapTool(tool.Name, serverName);

            _registry.Register(definition, async (input, innerCt) =>
            {
                var args = BuildArguments(input);
                var result = await capturedClient.CallToolAsync(capturedName, args, cancellationToken: innerCt)
                    .ConfigureAwait(false);

                if (result.Content is null || result.Content.Count == 0)
                    return string.Empty;

                var parts = result.Content
                    .OfType<TextContentBlock>()
                    .Select(c => c.Text ?? string.Empty);

                return string.Join(Environment.NewLine, parts);
            });
        }
    }

    private static Dictionary<string, object?> BuildArguments(JsonElement input)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (input.ValueKind != JsonValueKind.Object)
            return dict;

        foreach (var prop in input.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number when prop.Value.TryGetInt64(out var l) => (object?)l,
                JsonValueKind.Number => prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.GetRawText(),
            };
        }

        return dict;
    }

    /// <summary>
    /// Replaces the MCP client for a named server — used after OAuth token acquisition and by the
    /// Integrations connect flow. The old client is disposed, a fresh one is created with the
    /// updated config, and the server's tools are re-registered. Throws on failure (callers show
    /// their own message); the status registry is updated either way.
    /// </summary>
    public async Task ReconnectServerAsync(
        string serverName,
        McpServerConfig config,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        ArgumentNullException.ThrowIfNull(config);

        _configs[serverName] = config;
        CancelPendingRetry(serverName);
        try
        {
            await ConnectCoreAsync(serverName, config, ct).ConfigureAwait(false);
            _status.Set(new McpServerStatus(serverName, McpServerState.Connected));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Same handling as startup: one friendly line, and network failures keep retrying.
            var failure = McpConnectionError.Classify(serverName, config, ex);
            var retry = failure.ShouldRetry && RetryDelays.Count > 0;
            var status = new McpServerStatus(serverName, McpServerState.Unavailable, failure,
                NextRetryAt: retry ? DateTimeOffset.UtcNow + RetryDelays[0] : null, MaxAttempts: RetryDelays.Count);
            _status.Set(status);
            LogUnavailable(_logger, serverName, status.Message);
            LogFailureDetail(_logger, serverName, ex);
            if (retry)
                ScheduleRetry(serverName, RetryDelays[0], 1);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        foreach (var name in _pendingRetries.Keys.ToList())
            CancelPendingRetry(name);
        foreach (var client in _clients)
            await client.DisposeAsync().ConfigureAwait(false);
        _clients.Clear();
        _lifetime.Dispose();
        _clientsLock.Dispose();
    }
}
