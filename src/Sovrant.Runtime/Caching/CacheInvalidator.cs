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

    /// <summary>
    /// Invalidates all knowledge-store cache entries for <paramref name="kind"/> and any
    /// corresponding HTTP response cache keys (e.g. <c>skills:list</c>, <c>templates:list</c>).
    /// Called by <c>CachedKnowledgeStore</c> on every write so both the DB read cache and
    /// the route-level HTTP cache are evicted atomically.
    /// </summary>
    public Task InvalidateKnowledgeAsync(string kind, CancellationToken ct = default)
    {
        var tasks = new List<Task> { cache.RemoveByPrefixAsync($"knowledge:{kind}:", ct) };
        if (kind is "skills")
            tasks.Add(cache.RemoveByPrefixAsync("skills:", ct));
        else if (kind is "agents")
            tasks.Add(cache.RemoveByPrefixAsync("templates:", ct));
        return Task.WhenAll(tasks);
    }

    /// <summary>Invalidates all cache entries (nuclear option for testing or restarts).</summary>
    public Task InvalidateAllAsync(CancellationToken ct = default) =>
        cache.RemoveByPrefixAsync("", ct);
}
