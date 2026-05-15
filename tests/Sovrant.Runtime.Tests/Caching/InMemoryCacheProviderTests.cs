using Sovrant.Runtime.Caching;

namespace Sovrant.Runtime.Tests.Caching;

public sealed class InMemoryCacheProviderTests : IDisposable
{
    private readonly InMemoryCacheProvider _cache = new();

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyMissing()
    {
        var result = await _cache.GetAsync<string>("missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsCachedValue()
    {
        await _cache.SetAsync("key1", "value1", TimeSpan.FromMinutes(5));
        var result = await _cache.GetAsync<string>("key1");
        Assert.Equal("value1", result);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_AfterExpiry()
    {
        await _cache.SetAsync("key1", "value1", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);
        var result = await _cache.GetAsync<string>("key1");
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_EvictsEntry()
    {
        await _cache.SetAsync("key1", "value1", TimeSpan.FromMinutes(5));
        await _cache.RemoveAsync("key1");
        var result = await _cache.GetAsync<string>("key1");
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_EvictsMatchingEntries()
    {
        await _cache.SetAsync("tools:list", "a", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("tools:read_file", "b", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("skills:list", "c", TimeSpan.FromMinutes(5));

        await _cache.RemoveByPrefixAsync("tools:");

        Assert.Null(await _cache.GetAsync<string>("tools:list"));
        Assert.Null(await _cache.GetAsync<string>("tools:read_file"));
        Assert.Equal("c", await _cache.GetAsync<string>("skills:list"));
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingEntry()
    {
        await _cache.SetAsync("key1", "v1", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("key1", "v2", TimeSpan.FromMinutes(5));
        var result = await _cache.GetAsync<string>("key1");
        Assert.Equal("v2", result);
    }

    [Fact]
    public async Task GetAsync_WorksWithComplexTypes()
    {
        var obj = new TestDto { Name = "test", Count = 42 };
        await _cache.SetAsync("complex", obj, TimeSpan.FromMinutes(5));
        var result = await _cache.GetAsync<TestDto>("complex");
        Assert.NotNull(result);
        Assert.Equal("test", result.Name);
        Assert.Equal(42, result.Count);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_EmptyPrefix_ClearsAll()
    {
        await _cache.SetAsync("a", "1", TimeSpan.FromMinutes(5));
        await _cache.SetAsync("b", "2", TimeSpan.FromMinutes(5));
        await _cache.RemoveByPrefixAsync("");
        Assert.Null(await _cache.GetAsync<string>("a"));
        Assert.Null(await _cache.GetAsync<string>("b"));
    }

    [Fact]
    public async Task Keys_AreCaseInsensitive()
    {
        await _cache.SetAsync("Tools:List", "val", TimeSpan.FromMinutes(5));
        var result = await _cache.GetAsync<string>("tools:list");
        Assert.Equal("val", result);
    }

    private sealed class TestDto
    {
        public string Name { get; init; } = string.Empty;
        public int Count { get; init; }
    }
}
