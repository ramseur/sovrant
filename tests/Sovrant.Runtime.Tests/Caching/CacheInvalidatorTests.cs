using Sovrant.Runtime.Caching;

namespace Sovrant.Runtime.Tests.Caching;

public sealed class CacheInvalidatorTests : IDisposable
{
    private readonly InMemoryCacheProvider _cache = new();
    private readonly CacheInvalidator _invalidator;

    public CacheInvalidatorTests() => _invalidator = new CacheInvalidator(_cache);

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task InvalidateToolsAsync_RemovesToolEntries()
    {
        await _cache.SetAsync("tools:list", "a", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("tools:read_file", "b", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("skills:list", "c", TimeSpan.FromMinutes(5));

        await _invalidator.InvalidateToolsAsync();

        Assert.Null(await _cache.GetAsync<string>("tools:list"));
        Assert.Null(await _cache.GetAsync<string>("tools:read_file"));
        Assert.Equal("c", await _cache.GetAsync<string>("skills:list"));
    }

    [Fact]
    public async Task InvalidateSkillsAsync_RemovesSkillEntries()
    {
        await _cache.SetAsync("skills:list", "a", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("skills:my-skill", "b", TimeSpan.FromMinutes(5));

        await _invalidator.InvalidateSkillsAsync();

        Assert.Null(await _cache.GetAsync<string>("skills:list"));
        Assert.Null(await _cache.GetAsync<string>("skills:my-skill"));
    }

    [Fact]
    public async Task InvalidateTemplatesAsync_RemovesTemplateEntries()
    {
        await _cache.SetAsync("templates:list", "a", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("templates:researcher", "b", TimeSpan.FromMinutes(5));

        await _invalidator.InvalidateTemplatesAsync();

        Assert.Null(await _cache.GetAsync<string>("templates:list"));
        Assert.Null(await _cache.GetAsync<string>("templates:researcher"));
    }

    [Fact]
    public async Task InvalidateConfigAsync_RemovesConfigEntry()
    {
        await _cache.SetAsync("config:current", "cfg", TimeSpan.FromMinutes(5));

        await _invalidator.InvalidateConfigAsync();

        Assert.Null(await _cache.GetAsync<string>("config:current"));
    }

    [Fact]
    public async Task InvalidateStatusAsync_RemovesStatusEntry()
    {
        await _cache.SetAsync("status:current", "sts", TimeSpan.FromMinutes(5));

        await _invalidator.InvalidateStatusAsync();

        Assert.Null(await _cache.GetAsync<string>("status:current"));
    }

    [Fact]
    public async Task InvalidateAllAsync_ClearsEverything()
    {
        await _cache.SetAsync("tools:list", "a", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("skills:list", "b", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("config:current", "c", TimeSpan.FromMinutes(5));

        await _invalidator.InvalidateAllAsync();

        Assert.Null(await _cache.GetAsync<string>("tools:list"));
        Assert.Null(await _cache.GetAsync<string>("skills:list"));
        Assert.Null(await _cache.GetAsync<string>("config:current"));
    }
}
