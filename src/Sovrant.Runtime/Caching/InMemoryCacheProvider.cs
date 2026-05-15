using System.Collections.Concurrent;

namespace Sovrant.Runtime.Caching;

/// <summary>
/// In-memory cache backed by <see cref="ConcurrentDictionary{TKey,TValue}"/> with a periodic
/// background sweep that removes expired entries every 60 seconds.
/// </summary>
public sealed class InMemoryCacheProvider : ICacheProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _sweepTimer;

    public InMemoryCacheProvider()
    {
        _sweepTimer = new Timer(Sweep, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        if (_store.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return Task.FromResult((T?)entry.Value);

        // Expired or missing — clean up lazily.
        _store.TryRemove(key, out _);
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
    {
        var entry = new CacheEntry(value, DateTimeOffset.UtcNow + ttl);
        _store[key] = entry;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in _store.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                _store.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _sweepTimer.Dispose();
    }

    private void Sweep(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _store)
        {
            if (kvp.Value.ExpiresAt <= now)
                _store.TryRemove(kvp.Key, out _);
        }
    }

    private sealed record CacheEntry(object Value, DateTimeOffset ExpiresAt);
}
