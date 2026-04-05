namespace Sovrant.Runtime.Caching;

/// <summary>
/// Provides named invalidation methods that routes and mutation handlers can call
/// to evict stale cache entries after state changes.
/// </summary>
public sealed class CacheInvalidator(ICacheProvider cache)
{
    /// <summary>Invalidates all tool registry cache entries.</summary>
    public Task InvalidateToolsAsync(CancellationToken ct = default) =>
        cache.RemoveByPrefixAsync("tools:", ct);

    /// <summary>Invalidates all skill registry cache entries.</summary>
    public Task InvalidateSkillsAsync(CancellationToken ct = default) =>
        cache.RemoveByPrefixAsync("skills:", ct);

    /// <summary>Invalidates all agent template cache entries.</summary>
    public Task InvalidateTemplatesAsync(CancellationToken ct = default) =>
        cache.RemoveByPrefixAsync("templates:", ct);

    /// <summary>Invalidates the cached server config response.</summary>
    public Task InvalidateConfigAsync(CancellationToken ct = default) =>
        cache.RemoveAsync("config:current", ct);

    /// <summary>Invalidates the cached provider health/status response.</summary>
    public Task InvalidateStatusAsync(CancellationToken ct = default) =>
        cache.RemoveAsync("status:current", ct);

    /// <summary>Invalidates all cache entries (nuclear option for testing or restarts).</summary>
    public Task InvalidateAllAsync(CancellationToken ct = default) =>
        cache.RemoveByPrefixAsync("", ct);
}
