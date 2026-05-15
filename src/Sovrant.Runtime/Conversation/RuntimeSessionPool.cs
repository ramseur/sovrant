using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Hooks;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Memory;
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
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Phase 38 — include the owner in the pool key so two users cannot
        // share an in-memory runtime by picking the same session_id. The
        // persistence layer still stores the raw sessionId; only the pool
        // partition is owner-scoped.
        var pooledKey = ownerUserId is null ? sessionId : $"{sessionId}##{ownerUserId}";

        // Fast path — already in pool. Update last-access time.
        if (_pool.TryGetValue(pooledKey, out var existing))
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
                _services.GetRequiredService<ILogger<ConversationRuntime>>(),
                _services.GetService<IHookRunner>(),
                _services.GetService<MemoryInjector>(),
                settings: _services.GetService<Sovrant.Runtime.Workspaces.IWorkspaceSettingsStore>(),
                mcpClients: _services.GetService<McpClientRegistry>());
        }
        else
        {
            runtime = _services.GetRequiredService<IConversationRuntime>();
        }

        // Strip the composite key suffix for JSONL persistence.
        var persistenceId = sessionId.Contains("::", StringComparison.Ordinal)
            ? sessionId[..sessionId.IndexOf("::", StringComparison.Ordinal)]
            : sessionId;

        await runtime.InitializeSessionAsync(persistenceId, ownerUserId, ct).ConfigureAwait(false);

        var entry = new SessionEntry(runtime);
        var winner = _pool.GetOrAdd(pooledKey, entry);

        // Fire SessionStart only for the thread that actually created the entry.
        if (ReferenceEquals(winner, entry))
        {
            var hookRunner = _services.GetService<IHookRunner>();
            if (hookRunner is not null)
            {
                _ = hookRunner.RunAsync(
                    HookEvent.SessionStart,
                    new HookContext(HookEvent.SessionStart, persistenceId),
                    CancellationToken.None)
                    .ContinueWith(static t => { if (t.IsFaulted) _ = t.Exception; }, TaskScheduler.Default);
            }
        }

        // If another thread won the race, dispose the lock we just created.
        if (!ReferenceEquals(winner, entry))
            entry.Lock.Dispose();

        winner.Touch();
        return new PooledSession(winner.Runtime, winner.Lock, winner.Config);
    }

    /// <inheritdoc/>
    public void Evict(string sessionId, string? ownerUserId = null)
    {
        if (ownerUserId is not null)
        {
            var key = $"{sessionId}##{ownerUserId}";
            if (_pool.TryRemove(key, out var entry))
            {
                entry.Lock.Dispose();
                FireSessionEnd(sessionId);
            }
            return;
        }

        // Admin / legacy path — evict any entry whose pooled key resolves to
        // this session id, across all owners and provider-scoped variants.
        foreach (var kvp in _pool)
        {
            if (PooledKeyMatchesSessionId(kvp.Key, sessionId) &&
                _pool.TryRemove(kvp.Key, out var entry))
            {
                entry.Lock.Dispose();
                FireSessionEnd(sessionId);
            }
        }
    }

    private static bool PooledKeyMatchesSessionId(string pooledKey, string sessionId)
    {
        // Strip owner suffix ("##userId") and provider suffix ("::provider").
        var key = pooledKey;
        var hashIdx = key.IndexOf("##", StringComparison.Ordinal);
        if (hashIdx >= 0) key = key[..hashIdx];
        var colonIdx = key.IndexOf("::", StringComparison.Ordinal);
        if (colonIdx >= 0) key = key[..colonIdx];
        return string.Equals(key, sessionId, StringComparison.Ordinal);
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
                    FireSessionEnd(kvp.Key);
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
                    FireSessionEnd(sortedByAccess[i].Key);
                    evicted++;
                }
            }
        }

        return evicted;
    }

    /// <inheritdoc/>
    public SessionConfig? TryGetConfig(string sessionId, string? ownerUserId = null)
    {
        if (ownerUserId is not null)
        {
            var key = $"{sessionId}##{ownerUserId}";
            return _pool.TryGetValue(key, out var owned) ? owned.Config : null;
        }

        // Admin / legacy — first match wins. Usage and session-info endpoints
        // only need a live token-count snapshot, so any pool entry tracking
        // this session is acceptable.
        if (_pool.TryGetValue(sessionId, out var direct))
            return direct.Config;

        foreach (var kvp in _pool)
        {
            if (PooledKeyMatchesSessionId(kvp.Key, sessionId))
                return kvp.Value.Config;
        }

        return null;
    }

    private void FireSessionEnd(string sessionId)
    {
        var hookRunner = _services.GetService<IHookRunner>();
        if (hookRunner is null) return;

        // Strip composite key suffix for consistency with SessionStart.
        var persistenceId = sessionId.Contains("::", StringComparison.Ordinal)
            ? sessionId[..sessionId.IndexOf("::", StringComparison.Ordinal)]
            : sessionId;

        _ = hookRunner.RunAsync(
            HookEvent.SessionEnd,
            new HookContext(HookEvent.SessionEnd, persistenceId),
            CancellationToken.None)
            .ContinueWith(static t => { if (t.IsFaulted) _ = t.Exception; }, TaskScheduler.Default);

        // Fire-and-forget session summary extraction (Phase 25 memory system).
        var memoryHandler = _services.GetService<SessionEndMemoryHandler>();
        if (memoryHandler is not null)
            _ = memoryHandler.HandleSessionEndAsync(persistenceId);
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
