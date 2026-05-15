namespace Sovrant.Runtime.Caching;

/// <summary>
/// Provides a simple key-value cache with TTL-based expiration and prefix-based invalidation.
/// Default implementation is in-memory; a Redis adapter can be swapped in for multi-instance deployments.
/// </summary>
public interface ICacheProvider
{
    /// <summary>Gets a cached value by key, or <c>null</c> if not found or expired.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>Sets a value in the cache with the specified TTL.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;

    /// <summary>Removes a single entry by exact key.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Removes all entries whose key starts with <paramref name="prefix"/>.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
