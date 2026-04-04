using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Singleton pool that keeps one <see cref="IConversationRuntime"/> alive per session ID.
/// Each entry tracks a per-session lock and a last-access timestamp for TTL eviction.
/// </summary>
internal sealed class RuntimeSessionPool : IRuntimeSessionPool
{
    private readonly IServiceProvider _services;
    private readonly ConcurrentDictionary<string, SessionEntry> _pool = new(StringComparer.Ordinal);

    public RuntimeSessionPool(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    public int ActiveCount => _pool.Count;

    /// <inheritdoc/>
    public async Task<PooledSession> GetOrCreateAsync(
        string sessionId,
        ISmartRouter? scopedRouterOverride = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Fast path — already in pool. Update last-access time.
        if (_pool.TryGetValue(sessionId, out var existing))
        {
            existing.Touch();
            return new PooledSession(existing.Runtime, existing.Lock, existing.Config);
        }

        // Slow path — create, initialise, and race to insert.
        IConversationRuntime runtime;
        if (scopedRouterOverride is not null)
        {
            runtime = new ConversationRuntime(
                scopedRouterOverride,
                _services.GetRequiredService<IToolExecutor>(),
                _services.GetRequiredService<IToolRegistry>(),
                _services.GetRequiredService<ISessionStore>(),
                _services.GetRequiredService<SovrantConfig>(),
                _services.GetRequiredService<ILogger<ConversationRuntime>>());
        }
        else
        {
            runtime = _services.GetRequiredService<IConversationRuntime>();
        }

        // Strip the composite key suffix for JSONL persistence.
        var persistenceId = sessionId.Contains("::", StringComparison.Ordinal)
            ? sessionId[..sessionId.IndexOf("::", StringComparison.Ordinal)]
            : sessionId;

        await runtime.InitializeSessionAsync(persistenceId, ct).ConfigureAwait(false);

        var entry = new SessionEntry(runtime);
        var winner = _pool.GetOrAdd(sessionId, entry);

        // If another thread won the race, dispose the lock we just created.
        if (!ReferenceEquals(winner, entry))
            entry.Lock.Dispose();

        winner.Touch();
        return new PooledSession(winner.Runtime, winner.Lock, winner.Config);
    }

    /// <inheritdoc/>
    public void Evict(string sessionId)
    {
        if (_pool.TryRemove(sessionId, out var entry))
            entry.Lock.Dispose();
    }

    /// <inheritdoc/>
    public int EvictExpired(TimeSpan ttl, int maxSessions)
    {
        var evicted = 0;
        var cutoff = DateTimeOffset.UtcNow - ttl;

        // Phase 1: TTL eviction — remove sessions idle longer than TTL.
        foreach (var kvp in _pool)
        {
            if (kvp.Value.LastAccess < cutoff)
            {
                if (_pool.TryRemove(kvp.Key, out var removed))
                {
                    removed.Lock.Dispose();
                    evicted++;
                }
            }
        }

        // Phase 2: LRU cap — if still above max, evict least-recently-used.
        if (_pool.Count > maxSessions)
        {
            var sortedByAccess = _pool
                .OrderBy(kvp => kvp.Value.LastAccess)
                .ToList();

            var excess = _pool.Count - maxSessions;
            for (var i = 0; i < excess && i < sortedByAccess.Count; i++)
            {
                if (_pool.TryRemove(sortedByAccess[i].Key, out var removed))
                {
                    removed.Lock.Dispose();
                    evicted++;
                }
            }
        }

        return evicted;
    }

    /// <inheritdoc/>
    public SessionConfig? TryGetConfig(string sessionId)
    {
        return _pool.TryGetValue(sessionId, out var entry) ? entry.Config : null;
    }

    private sealed class SessionEntry
    {
        public IConversationRuntime Runtime { get; }
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public SessionConfig Config { get; } = new();
        public DateTimeOffset LastAccess { get; private set; } = DateTimeOffset.UtcNow;

        public SessionEntry(IConversationRuntime runtime)
        {
            Runtime = runtime;
        }

        public void Touch() => LastAccess = DateTimeOffset.UtcNow;
    }
}
