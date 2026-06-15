using Sovrant.Runtime.Caching;
using Sovrant.Runtime.Knowledge;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class CachedKnowledgeStoreTests : IDisposable
{
    private readonly InMemoryCacheProvider _cache = new();
    private readonly CacheInvalidator _invalidator;
    private readonly FakeKnowledgeStore _inner = new();
    private readonly CachedKnowledgeStore _store;

    public CachedKnowledgeStoreTests()
    {
        _invalidator = new CacheInvalidator(_cache);
        _store = new CachedKnowledgeStore(_inner, _cache, _invalidator);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetAllEffectiveAsync_SecondCall_DoesNotHitInnerStore()
    {
        _inner.Seed("skills", MakePage("skills", "alpha"));

        await _store.GetAllEffectiveAsync("skills");
        await _store.GetAllEffectiveAsync("skills");

        Assert.Equal(1, _inner.GetAllEffectiveCallCount);
    }

    [Fact]
    public async Task GetEffectiveAsync_SecondCall_DoesNotHitInnerStore()
    {
        _inner.Seed("skills", MakePage("skills", "alpha"));

        await _store.GetEffectiveAsync("skills", "alpha");
        await _store.GetEffectiveAsync("skills", "alpha");

        Assert.Equal(1, _inner.GetEffectiveCallCount);
    }

    [Fact]
    public async Task GetEffectiveAsync_MissingSlug_CachesNullSentinel()
    {
        // Two calls for a non-existent slug — inner should only be hit once.
        await _store.GetEffectiveAsync("skills", "missing");
        await _store.GetEffectiveAsync("skills", "missing");

        Assert.Equal(1, _inner.GetEffectiveCallCount);
    }

    [Fact]
    public async Task UpsertAsync_EvictsCache_NextCallHitsInnerStore()
    {
        _inner.Seed("skills", MakePage("skills", "alpha"));
        await _store.GetAllEffectiveAsync("skills");

        var updated = MakePage("skills", "alpha", body: "updated body");
        await _store.UpsertAsync(updated);
        _inner.Seed("skills", updated);

        var result = await _store.GetAllEffectiveAsync("skills");
        Assert.Equal(2, _inner.GetAllEffectiveCallCount);
        Assert.Equal("updated body", result[0].Body);
    }

    [Fact]
    public async Task UpsertAsync_SkillsKind_EvictsHttpResponseCacheKeys()
    {
        // Simulate HTTP response cache entries written by SkillRegistryRoutes.
        await _cache.SetAsync("skills:list", "cached-list", TimeSpan.FromHours(1));
        await _cache.SetAsync("skills:alpha", "cached-alpha", TimeSpan.FromHours(1));

        await _store.UpsertAsync(MakePage("skills", "alpha"));

        Assert.Null(await _cache.GetAsync<string>("skills:list"));
        Assert.Null(await _cache.GetAsync<string>("skills:alpha"));
    }

    [Fact]
    public async Task UpsertAsync_AgentsKind_EvictsTemplatesHttpResponseCacheKeys()
    {
        await _cache.SetAsync("templates:list", "cached-list", TimeSpan.FromHours(1));
        await _cache.SetAsync("templates:researcher", "cached-researcher", TimeSpan.FromHours(1));

        await _store.UpsertAsync(MakePage("agents", "researcher"));

        Assert.Null(await _cache.GetAsync<string>("templates:list"));
        Assert.Null(await _cache.GetAsync<string>("templates:researcher"));
    }

    [Fact]
    public async Task DeleteAsync_EvictsCache_NextCallHitsInnerStore()
    {
        _inner.Seed("skills", MakePage("skills", "alpha"));
        await _store.GetAllEffectiveAsync("skills");

        await _store.DeleteAsync("skills", "alpha", "global");
        _inner.RemoveSeeded("skills", "alpha");

        var result = await _store.GetAllEffectiveAsync("skills");
        Assert.Equal(2, _inner.GetAllEffectiveCallCount);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_PassesThroughWithoutCaching()
    {
        _inner.Seed("skills", MakePage("skills", "alpha"));

        await _store.GetAllAsync("skills");
        await _store.GetAllAsync("skills");

        Assert.Equal(2, _inner.GetAllCallCount);
    }

    private static KnowledgePage MakePage(string kind, string slug, string body = "body") =>
        new(
            KnowledgeId:  $"{kind}_{slug}",
            Kind:         kind,
            Slug:         slug,
            Name:         slug,
            Description:  string.Empty,
            Tier:         "User",
            Body:         body,
            WorkspaceId:  "global",
            CreatedAt:    DateTimeOffset.UtcNow,
            UpdatedAt:    DateTimeOffset.UtcNow);
}

/// <summary>Minimal fake that counts calls and holds an in-memory seed list.</summary>
internal sealed class FakeKnowledgeStore : IKnowledgeStore
{
    private readonly Dictionary<string, List<KnowledgePage>> _data = new(StringComparer.OrdinalIgnoreCase);

    public int GetAllCallCount { get; private set; }
    public int GetEffectiveCallCount { get; private set; }
    public int GetAllEffectiveCallCount { get; private set; }

    public void Seed(string kind, KnowledgePage page)
    {
        if (!_data.TryGetValue(kind, out var list))
            _data[kind] = list = [];
        list.RemoveAll(p => p.Slug == page.Slug);
        list.Add(page);
    }

    public void RemoveSeeded(string kind, string slug)
    {
        if (_data.TryGetValue(kind, out var list))
            list.RemoveAll(p => p.Slug == slug);
    }

    public Task<IReadOnlyList<KnowledgePage>> GetAllAsync(string kind, string workspaceId = "", CancellationToken ct = default)
    {
        GetAllCallCount++;
        IReadOnlyList<KnowledgePage> result = _data.TryGetValue(kind, out var list) ? list : [];
        return Task.FromResult(result);
    }

    public Task<KnowledgePage?> GetAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default) =>
        Task.FromResult<KnowledgePage?>(null);

    public Task<KnowledgePage?> GetActiveAsync(string kind, string slug, CancellationToken ct = default) =>
        Task.FromResult<KnowledgePage?>(null);

    public Task<KnowledgePage?> GetEffectiveAsync(string kind, string slug, string projectId = "", CancellationToken ct = default)
    {
        GetEffectiveCallCount++;
        var page = _data.TryGetValue(kind, out var list) ? list.FirstOrDefault(p => p.Slug == slug) : null;
        return Task.FromResult(page);
    }

    public Task<IReadOnlyList<KnowledgePage>> GetAllEffectiveAsync(string kind, string projectId = "", CancellationToken ct = default)
    {
        GetAllEffectiveCallCount++;
        IReadOnlyList<KnowledgePage> result = _data.TryGetValue(kind, out var list) ? list : [];
        return Task.FromResult(result);
    }

    public Task UpsertAsync(KnowledgePage page, CancellationToken ct = default)
    {
        Seed(page.Kind, page);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default)
    {
        RemoveSeeded(kind, slug);
        return Task.CompletedTask;
    }
}
