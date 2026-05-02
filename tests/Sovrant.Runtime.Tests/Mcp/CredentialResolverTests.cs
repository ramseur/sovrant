using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>
/// Tests for <see cref="CredentialResolver"/> — the env &gt; store &gt; fallback chain
/// used by Brave / FireCrawl / OpenRouter / Provider key consumers.
/// </summary>
public sealed class CredentialResolverTests : IDisposable
{
    private const string Env = "SOVRANT_TEST_KEY";
    private readonly string? _orig = Environment.GetEnvironmentVariable(Env);

    public CredentialResolverTests() => Environment.SetEnvironmentVariable(Env, null);
    public void Dispose() => Environment.SetEnvironmentVariable(Env, _orig);

    [Fact]
    public async Task EnvVar_BeatsStoreAndFallback()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreAsync("k", "from-store");
        Environment.SetEnvironmentVariable(Env, "from-env");

        var result = await CredentialResolver.ResolveAsync(store, "k", Env, fallback: "from-fallback");
        Assert.Equal("from-env", result);
    }

    [Fact]
    public async Task StoreValue_BeatsFallback()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreAsync("k", "from-store");

        var result = await CredentialResolver.ResolveAsync(store, "k", Env, fallback: "from-fallback");
        Assert.Equal("from-store", result);
    }

    [Fact]
    public async Task Fallback_UsedWhenNothingElseSet()
    {
        var store = new InMemoryCredentialStore();
        var result = await CredentialResolver.ResolveAsync(store, "k", Env, fallback: "from-fallback");
        Assert.Equal("from-fallback", result);
    }

    [Fact]
    public async Task ReturnsNull_WhenNothingResolves()
    {
        var store = new InMemoryCredentialStore();
        var result = await CredentialResolver.ResolveAsync(store, "k", Env, fallback: null);
        Assert.Null(result);
    }

    [Fact]
    public async Task NullStore_StillReadsEnvAndFallback()
    {
        Environment.SetEnvironmentVariable(Env, "from-env");
        var result = await CredentialResolver.ResolveAsync(store: null, "k", Env, fallback: "from-fallback");
        Assert.Equal("from-env", result);

        Environment.SetEnvironmentVariable(Env, null);
        result = await CredentialResolver.ResolveAsync(store: null, "k", Env, fallback: "from-fallback");
        Assert.Equal("from-fallback", result);
    }

    [Fact]
    public async Task EmptyEnvVar_TreatedAsUnset()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreAsync("k", "from-store");
        Environment.SetEnvironmentVariable(Env, string.Empty);

        var result = await CredentialResolver.ResolveAsync(store, "k", Env, fallback: "from-fallback");
        Assert.Equal("from-store", result);
    }

    [Fact]
    public async Task StoreThrowing_FallsThroughToFallback()
    {
        var store = new ThrowingCredentialStore();
        var result = await CredentialResolver.ResolveAsync(store, "k", Env, fallback: "from-fallback");
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
