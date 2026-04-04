using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sovrant.Lsp.JsonRpc;

/// <summary>
/// Bidirectional JSON-RPC 2.0 transport over stdio streams using
/// <c>Content-Length</c> framing (the LSP base protocol).
/// </summary>
internal sealed class JsonRpcTransport : IAsyncDisposable
{
    // Streams are owned by the Process — not disposed here.
#pragma warning disable CA2213
    private readonly Stream _input;
    private readonly Stream _output;
#pragma warning restore CA2213
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readLoop;

    private int _nextId;

    /// <summary>Fired when the server sends a notification (no id).</summary>
    public event Action<string, JsonElement?>? OnNotification;

    public JsonRpcTransport(Stream input, Stream output, ILogger logger)
    {
        _input = input;
        _output = output;
        _logger = logger;
        _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
    }

    /// <summary>Sends a request and waits for the matching response.</summary>
    public async Task<JsonRpcResponse> SendRequestAsync(string method, object? @params, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var request = new JsonRpcRequest { Id = id, Method = method, Params = @params };
        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        _pending[id] = tcs;

        try
        {
            await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions.Default), ct)
                .ConfigureAwait(false);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            linked.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    /// <summary>Sends a notification (no response expected).</summary>
    public async Task SendNotificationAsync(string method, object? @params, CancellationToken ct = default)
    {
        var notification = new JsonRpcNotification { Method = method, Params = @params };
        await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(notification, JsonOptions.Default), ct)
            .ConfigureAwait(false);
    }

    private async Task WriteMessageAsync(byte[] json, CancellationToken ct)
    {
        var header = Encoding.ASCII.GetBytes($"Content-Length: {json.Length}\r\n\r\n");

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _output.WriteAsync(header, ct).ConfigureAwait(false);
            await _output.WriteAsync(json, ct).ConfigureAwait(false);
            await _output.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var contentLength = await ReadHeadersAsync(ct).ConfigureAwait(false);
                if (contentLength <= 0) break;

                var body = new byte[contentLength];
                var offset = 0;
                while (offset < contentLength)
                {
                    var read = await _input.ReadAsync(body.AsMemory(offset, contentLength - offset), ct)
                        .ConfigureAwait(false);
                    if (read == 0) return; // stream closed
                    offset += read;
                }

                DispatchMessage(body);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON-RPC read loop error");
        }
        finally
        {
            // Cancel all pending requests.
            foreach (var kvp in _pending)
                kvp.Value.TrySetCanceled(CancellationToken.None);
        }
    }

    private async Task<int> ReadHeadersAsync(CancellationToken ct)
    {
        // Read headers line-by-line until we hit an empty line.
        var contentLength = -1;
        while (true)
        {
            var line = await ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) return -1; // stream closed
            if (line.Length == 0) break; // end of headers

            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["Content-Length:".Length..].Trim();
                if (int.TryParse(value, out var len))
                    contentLength = len;
            }
        }
        return contentLength;
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new byte[1];
        while (true)
        {
            var read = await _input.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read == 0) return sb.Length > 0 ? sb.ToString() : null;

            var c = (char)buffer[0];
            if (c == '\n')
            {
                // Strip trailing \r if present.
                if (sb.Length > 0 && sb[^1] == '\r')
                    sb.Length--;
                return sb.ToString();
            }
            sb.Append(c);
        }
    }

    private void DispatchMessage(byte[] body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var idProp))
            {
                // Response — match to pending request.
                var response = JsonSerializer.Deserialize<JsonRpcResponse>(body, JsonOptions.Default);
                if (response is not null && _pending.TryRemove(response.Id, out var tcs))
                    tcs.TrySetResult(response);
            }
            else if (root.TryGetProperty("method", out var methodProp))
            {
                // Notification from server.
                var method = methodProp.GetString() ?? string.Empty;
                JsonElement? @params = root.TryGetProperty("params", out var p) ? p.Clone() : null;
                OnNotification?.Invoke(method, @params);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON-RPC message");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        try { await _readLoop.ConfigureAwait(false); }
        catch (OperationCanceledException) { }

        _cts.Dispose();
        _writeLock.Dispose();
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
