using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Server.Hubs;

/// <summary>
/// SignalR hub for real-time chat streaming. Supports full-duplex communication
/// including tool confirmation round-trips that SSE cannot provide.
/// <para>Authentication via <c>?access_token=</c> query string (standard SignalR
/// pattern for WebSocket transport).</para>
/// </summary>
public sealed class ChatHub : Hub
{
    private readonly IRuntimeSessionPool _sessionPool;
    private readonly ILogger<ChatHub> _logger;

    /// <summary>
    /// Pending tool confirmation requests keyed by <c>{connectionId}:{toolUseId}</c>.
    /// The server pauses the agentic loop, emits a confirmation request, and waits
    /// for the client to call <see cref="ConfirmTool"/> or <see cref="DenyTool"/>.
    /// </summary>
    internal static readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> PendingConfirmations = new();

    public ChatHub(IRuntimeSessionPool sessionPool, ILogger<ChatHub> logger)
    {
        _sessionPool = sessionPool;
        _logger = logger;
    }

    /// <summary>
    /// Streams a conversation turn. The client receives <see cref="RuntimeEventDto"/>
    /// instances as they are produced by the runtime.
    /// </summary>
    public async IAsyncEnumerable<RuntimeEventDto> StreamTurn(
        string sessionId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userMessage))
        {
            yield return new RuntimeEventDto { Type = "runtime_error", Message = "sessionId and userMessage are required." };
            yield break;
        }

        // Resolve user identity from the hub context (set by BearerTokenMiddleware on negotiate).
        var userId = Context.GetHttpContext()?.Items[Auth.SovrantHttpContextKeys.UserId] as string;

        PooledSession? pooled = null;
        string? sessionError = null;
        try
        {
            pooled = await _sessionPool.GetOrCreateAsync(sessionId, ownerUserId: userId, ct: ct);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get/create session {SessionId}", sessionId);
            sessionError = "Failed to create session.";
        }

        if (pooled is null)
        {
            yield return new RuntimeEventDto { Type = "runtime_error", Message = sessionError ?? "Session unavailable." };
            yield break;
        }

        await pooled.Lock.WaitAsync(ct);
        try
        {
            await foreach (var ev in pooled.Runtime.RunTurnAsync(userMessage, ct))
            {
                yield return RuntimeEventDto.FromEvent(ev);
            }
        }
        finally
        {
            pooled.Lock.Release();
        }
    }

    /// <summary>Approves a pending tool confirmation request.</summary>
    public void ConfirmTool(string toolUseId)
    {
        var key = $"{Context.ConnectionId}:{toolUseId}";
        if (PendingConfirmations.TryRemove(key, out var tcs))
            tcs.TrySetResult(true);
    }

    /// <summary>Denies a pending tool confirmation request.</summary>
    public void DenyTool(string toolUseId)
    {
        var key = $"{Context.ConnectionId}:{toolUseId}";
        if (PendingConfirmations.TryRemove(key, out var tcs))
            tcs.TrySetResult(false);
    }

    /// <summary>Cancels the in-progress turn for the specified session.</summary>
    public void CancelTurn(string sessionId)
    {
        _logger.LogInformation("Client requested turn cancellation for session {SessionId}", sessionId);
        // Cancellation is handled via the CancellationToken passed to StreamTurn —
        // the client disconnecting or calling this triggers cooperative cancellation
        // through the hub's connection abort.
        Context.Abort();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up any pending confirmations for this connection.
        var prefix = $"{Context.ConnectionId}:";
        foreach (var key in PendingConfirmations.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) &&
                PendingConfirmations.TryRemove(key, out var tcs))
            {
                tcs.TrySetResult(false); // Default to deny on disconnect.
            }
        }

        return base.OnDisconnectedAsync(exception);
    }
}
