using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>
/// Tests for <see cref="CredentialResolver"/> — store → fallback chain.
/// API keys are never read from environment variables.
/// </summary>
public sealed class CredentialResolverTests
{
    [Fact]
    public async Task StoreValue_BeatsFallback()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreAsync("k", "from-store");

        var result = await CredentialResolver.ResolveAsync(store, "k", fallback: "from-fallback");
        Assert.Equal("from-store", result);
    }

    [Fact]
    public async Task Fallback_UsedWhenStoreEmpty()
    {
        var store = new InMemoryCredentialStore();
        var result = await CredentialResolver.ResolveAsync(store, "k", fallback: "from-fallback");
        Assert.Equal("from-fallback", result);
    }

    [Fact]
    public async Task ReturnsNull_WhenNothingResolves()
    {
        var store = new InMemoryCredentialStore();
        var result = await CredentialResolver.ResolveAsync(store, "k", fallback: null);
        Assert.Null(result);
    }

    [Fact]
    public async Task NullStore_ReturnsFallback()
    {
        var result = await CredentialResolver.ResolveAsync(store: null, "k", fallback: "from-fallback");
        Assert.Equal("from-fallback", result);

        result = await CredentialResolver.ResolveAsync(store: null, "k", fallback: null);
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreThrowing_FallsThroughToFallback()
    {
        var store = new ThrowingCredentialStore();
        var result = await CredentialResolver.ResolveAsync(store, "k", fallback: "from-fallback");
        Assert.Equal("from-fallback", result);
    }

    private sealed class InMemoryCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

        public Task StoreAsync(string key, string value, CancellationToken ct = default)
        {
            _data[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> RetrieveAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_data.TryGetValue(key, out var v) ? v : null);

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            _data.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCredentialStore : ICredentialStore
    {
        public Task StoreAsync(string key, string value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> RetrieveAsync(string key, CancellationToken ct = default)
            => throw new IOException("simulated store failure");
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }
}
