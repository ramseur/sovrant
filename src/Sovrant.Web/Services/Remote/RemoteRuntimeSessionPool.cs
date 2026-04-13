using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Web.Services.Remote;

/// <summary>
/// <see cref="IRuntimeSessionPool"/> that delegates to a remote Sovrant server via SignalR.
/// Each session gets a local <see cref="SemaphoreSlim"/> for client-side turn serialization
/// and a <see cref="RemoteConversationRuntime"/> that streams events from the server.
/// </summary>
public sealed class RemoteRuntimeSessionPool : IRuntimeSessionPool
{
    private readonly ConcurrentDictionary<string, RemoteSessionEntry> _pool = new(StringComparer.Ordinal);
    private readonly SignalRStreamingClient _signalR;

    public RemoteRuntimeSessionPool(SignalRStreamingClient signalR)
    {
        _signalR = signalR;
    }

    public int ActiveCount => _pool.Count;

    public Task<PooledSession> GetOrCreateAsync(
        string sessionId,
        ISmartRouter? scopedRouterOverride = null,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        var key = ownerUserId is not null ? $"{sessionId}###{ownerUserId}" : sessionId;

        var entry = _pool.GetOrAdd(key, _ => new RemoteSessionEntry(
            new RemoteConversationRuntime(_signalR, sessionId)));

        entry.Touch();
        return Task.FromResult(new PooledSession(entry.Runtime, entry.Lock, entry.Config));
    }

    public void Evict(string sessionId, string? ownerUserId = null)
    {
        var key = ownerUserId is not null ? $"{sessionId}###{ownerUserId}" : sessionId;
        _pool.TryRemove(key, out _);
    }

    public int EvictExpired(TimeSpan ttl, int maxSessions)
    {
        var now = DateTimeOffset.UtcNow;
        var evicted = 0;

        foreach (var (key, entry) in _pool)
        {
            if (now - entry.LastAccess > ttl && _pool.TryRemove(key, out _))
                evicted++;
        }

        return evicted;
    }

    public SessionConfig? TryGetConfig(string sessionId, string? ownerUserId = null)
    {
        var key = ownerUserId is not null ? $"{sessionId}###{ownerUserId}" : sessionId;
        return _pool.TryGetValue(key, out var entry) ? entry.Config : null;
    }

    private sealed class RemoteSessionEntry
    {
        public IConversationRuntime Runtime { get; }
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public SessionConfig Config { get; } = new();
        public DateTimeOffset LastAccess { get; private set; } = DateTimeOffset.UtcNow;

        public RemoteSessionEntry(IConversationRuntime runtime) => Runtime = runtime;
        public void Touch() => LastAccess = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// <see cref="IConversationRuntime"/> that streams events from a remote server via SignalR.
/// </summary>
public sealed class RemoteConversationRuntime : IConversationRuntime
{
    private readonly SignalRStreamingClient _signalR;
    private readonly string _sessionId;

    public RemoteConversationRuntime(SignalRStreamingClient signalR, string sessionId)
    {
        _signalR = signalR;
        _sessionId = sessionId;
    }

    public string SessionId => _sessionId;

    public async IAsyncEnumerable<RuntimeEvent> RunTurnAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var dto in _signalR.StreamTurnAsync(_sessionId, userMessage, ct))
        {
            var ev = dto.ToEvent();
            if (ev is not null)
                yield return ev;
        }
    }

    public Task InitializeSessionAsync(string? sessionId, string? ownerUserId = null, CancellationToken ct = default)
    {
        // Session initialization happens server-side.
        return Task.CompletedTask;
    }

    public void Reset()
    {
        // No-op in remote mode — the server manages session state.
    }
}
