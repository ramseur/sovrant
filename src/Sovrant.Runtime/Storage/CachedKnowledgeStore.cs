using Sovrant.Runtime.Caching;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Decorator over <see cref="IKnowledgeStore"/> that caches <see cref="GetEffectiveAsync"/> and
/// <see cref="GetAllEffectiveAsync"/> results in <see cref="ICacheProvider"/> with per-kind TTLs.
/// On any write (<see cref="UpsertAsync"/> / <see cref="DeleteAsync"/>) the affected kind's cache
/// entries are evicted synchronously before returning, so the authoring user always reads their own
/// writes. Set <c>SOVRANT_KNOWLEDGE_CACHE_TTL=0</c> to skip this decorator entirely (see DI wiring
/// in <c>ServiceCollectionExtensions</c>).
/// </summary>
internal sealed class CachedKnowledgeStore(
    IKnowledgeStore inner,
    ICacheProvider cache,
    CacheInvalidator invalidator) : IKnowledgeStore
{
    // Pass-through: GetAllAsync and GetActiveAsync are low-level / internal reads not used by
    // the registries, so we don't cache them — only the two overlay-resolution methods matter.
    public Task<IReadOnlyList<KnowledgePage>> GetAllAsync(string kind, string workspaceId = "", CancellationToken ct = default) =>
        inner.GetAllAsync(kind, workspaceId, ct);

    public Task<KnowledgePage?> GetAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default) =>
        inner.GetAsync(kind, slug, workspaceId, ct);

    public Task<KnowledgePage?> GetActiveAsync(string kind, string slug, CancellationToken ct = default) =>
        inner.GetActiveAsync(kind, slug, ct);

    public async Task<KnowledgePage?> GetEffectiveAsync(string kind, string slug, string projectId = "", CancellationToken ct = default)
    {
        var key = EffectiveKey(kind, slug, projectId);
        var cached = await cache.GetAsync<NullablePageEntry>(key, ct).ConfigureAwait(false);
        if (cached is not null)
            return cached.Page;

        var page = await inner.GetEffectiveAsync(kind, slug, projectId, ct).ConfigureAwait(false);
        await cache.SetAsync(key, new NullablePageEntry(page), CacheTtl.ForKnowledgeKind(kind), ct).ConfigureAwait(false);
        return page;
    }

    public async Task<IReadOnlyList<KnowledgePage>> GetAllEffectiveAsync(string kind, string projectId = "", CancellationToken ct = default)
    {
        var key = AllEffectiveKey(kind, projectId);
        var cached = await cache.GetAsync<PageListEntry>(key, ct).ConfigureAwait(false);
        if (cached is not null)
            return cached.Pages;

        var pages = await inner.GetAllEffectiveAsync(kind, projectId, ct).ConfigureAwait(false);
        await cache.SetAsync(key, new PageListEntry(pages), CacheTtl.ForKnowledgeKind(kind), ct).ConfigureAwait(false);
        return pages;
    }

    public async Task UpsertAsync(KnowledgePage page, CancellationToken ct = default)
    {
        await inner.UpsertAsync(page, ct).ConfigureAwait(false);
        await invalidator.InvalidateKnowledgeAsync(page.Kind, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default)
    {
        await inner.DeleteAsync(kind, slug, workspaceId, ct).ConfigureAwait(false);
        await invalidator.InvalidateKnowledgeAsync(kind, ct).ConfigureAwait(false);
    }

    private static string EffectiveKey(string kind, string slug, string projectId) =>
        $"knowledge:{kind}:{slug}:{projectId}";

    private static string AllEffectiveKey(string kind, string projectId) =>
        $"knowledge:{kind}:all:{projectId}";

    // Wrapper records so that a null KnowledgePage result can be cached (T : class constraint
    // on ICacheProvider means a null T is indistinguishable from a cache miss without a wrapper).
    private sealed record NullablePageEntry(KnowledgePage? Page);
    private sealed record PageListEntry(IReadOnlyList<KnowledgePage> Pages);
}
