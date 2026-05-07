using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Client.Remote;

/// <summary>
/// Manages the SignalR connection to the Sovrant server's <c>/hubs/chat</c> endpoint.
/// Provides streaming turn execution and handles reconnection with exponential backoff.
/// </summary>
public sealed class SignalRStreamingClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly RemoteConnectionState _connectionState;
    private readonly ILogger<SignalRStreamingClient> _logger;

    public SignalRStreamingClient(
        SovrantRemoteOptions options,
        RemoteConnectionState connectionState,
        ILogger<SignalRStreamingClient> logger)
    {
        _connectionState = connectionState;
        _logger = logger;

        var hubUrl = $"{options.Url?.TrimEnd('/')}/hubs/chat";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, httpOptions =>
            {
                // Lambda reads options.ApiToken at call time so token hot-swap works after re-auth.
                httpOptions.AccessTokenProvider = () => Task.FromResult(options.ApiToken);
            })
            .WithAutomaticReconnect(BuildRetryPolicy(options.SignalR))
            .Build();

        _connection.Reconnecting += _ =>
        {
            _connectionState.Status = ConnectionStatus.Reconnecting;
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            _connectionState.Status = ConnectionStatus.Connected;
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            _connectionState.Status = ConnectionStatus.Disconnected;
            return Task.CompletedTask;
        };
    }

    /// <summary>Ensures the connection is started.</summary>
    public async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (_connection.State == HubConnectionState.Connected)
            return;

        if (_connection.State == HubConnectionState.Disconnected)
        {
            await _connection.StartAsync(ct);
            _connectionState.Status = ConnectionStatus.Connected;
            _logger.LogInformation("SignalR connected to chat hub");
        }
    }

    /// <summary>
    /// Streams a conversation turn, yielding <see cref="RuntimeEventDto"/> as they arrive.
    /// </summary>
    public async IAsyncEnumerable<RuntimeEventDto> StreamTurnAsync(
        string sessionId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);

        await foreach (var dto in _connection.StreamAsync<RuntimeEventDto>(
            "StreamTurn", sessionId, userMessage, ct))
        {
            yield return dto;
        }
    }

    /// <summary>Confirms a pending tool execution.</summary>
    public async Task ConfirmToolAsync(string toolUseId, CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        await _connection.InvokeAsync("ConfirmTool", toolUseId, ct);
    }

    /// <summary>Denies a pending tool execution.</summary>
    public async Task DenyToolAsync(string toolUseId, CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        await _connection.InvokeAsync("DenyTool", toolUseId, ct);
    }

    /// <summary>Cancels the current turn for a session.</summary>
    public async Task CancelTurnAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        await _connection.InvokeAsync("CancelTurn", sessionId, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private static ExponentialRetryPolicy BuildRetryPolicy(SignalROptions options)
    {
        return new ExponentialRetryPolicy(options.ReconnectIntervalMs, options.MaxReconnectAttempts);
    }

    private sealed class ExponentialRetryPolicy : IRetryPolicy
    {
        private readonly int _baseMs;
        private readonly int _maxAttempts;

        public ExponentialRetryPolicy(int baseMs, int maxAttempts)
        {
            _baseMs = baseMs;
            _maxAttempts = maxAttempts;
        }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext.PreviousRetryCount >= _maxAttempts)
                return null;

            var delay = _baseMs * Math.Pow(1.5, retryContext.PreviousRetryCount);
            return TimeSpan.FromMilliseconds(Math.Min(delay, 60_000));
        }
    }
}
