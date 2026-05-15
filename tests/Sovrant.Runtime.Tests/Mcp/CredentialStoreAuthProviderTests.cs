using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>
/// Tests for <see cref="CredentialStoreAuthProvider"/>. Verifies the
/// env &gt; store &gt; static-fallback precedence chain that the CLI / Server
/// rely on when no Web/Desktop <c>MutableAuthProvider</c> overrides it.
/// </summary>
public sealed class CredentialStoreAuthProviderTests : IDisposable
{
    private const string CredKey = "llm.api_key";
    private const string EnvVar = "LLM_API_KEY";
    private const string AltEnv = "OPENAI_API_KEY";

    private readonly string? _origLlm = Environment.GetEnvironmentVariable(EnvVar);
    private readonly string? _origAlt = Environment.GetEnvironmentVariable(AltEnv);
    private readonly string? _origProv = Environment.GetEnvironmentVariable("PROVIDER_API_KEY");

    public CredentialStoreAuthProviderTests()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);
        Environment.SetEnvironmentVariable(AltEnv, null);
        Environment.SetEnvironmentVariable("PROVIDER_API_KEY", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvVar, _origLlm);
        Environment.SetEnvironmentVariable(AltEnv, _origAlt);
        Environment.SetEnvironmentVariable("PROVIDER_API_KEY", _origProv);
    }

    [Fact]
    public async Task EnvVar_BeatsStore()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreAsync(CredKey, "from-store");
        Environment.SetEnvironmentVariable(EnvVar, "from-env");

        var provider = new CredentialStoreAuthProvider(store, CredKey, fallback: "from-fallback");
        var token = await provider.GetAuthHeaderAsync(CancellationToken.None);

        Assert.Equal("from-env", token);
    }

    [Fact]
    public async Task StoreValue_UsedWhenNoEnvSet()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreAsync(CredKey, "from-store");

        var provider = new CredentialStoreAuthProvider(store, CredKey, fallback: "from-fallback");
        var token = await provider.GetAuthHeaderAsync(CancellationToken.None);

        Assert.Equal("from-store", token);
    }

    [Fact]
    public async Task Fallback_UsedWhenStoreEmptyAndNoEnv()
    {
        var store = new InMemoryCredentialStore();

        var provider = new CredentialStoreAuthProvider(store, CredKey, fallback: "from-fallback");
        var token = await provider.GetAuthHeaderAsync(CancellationToken.None);

        Assert.Equal("from-fallback", token);
    }

    [Fact]
    public async Task EmptyEnvVar_TreatedAsUnset_StoreUsed()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreAsync(CredKey, "from-store");
        Environment.SetEnvironmentVariable(EnvVar, string.Empty);

        var provider = new CredentialStoreAuthProvider(store, CredKey, fallback: "from-fallback");
        var token = await provider.GetAuthHeaderAsync(CancellationToken.None);

        Assert.Equal("from-store", token);
    }

    [Fact]
    public async Task SecondaryEnvAlias_AlsoChecked()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreAsync(CredKey, "from-store");
        Environment.SetEnvironmentVariable(AltEnv, "from-openai-alias");

        var provider = new CredentialStoreAuthProvider(store, CredKey, fallback: "from-fallback");
        var token = await provider.GetAuthHeaderAsync(CancellationToken.None);

        Assert.Equal("from-openai-alias", token);
    }

    [Fact]
    public async Task Invalidate_ForcesStoreReread()
    {
        var store = new InMemoryCredentialStore();
        await store.StoreAsync(CredKey, "first");

        var provider = new CredentialStoreAuthProvider(store, CredKey, fallback: "from-fallback");
        Assert.Equal("first", await provider.GetAuthHeaderAsync(CancellationToken.None));

        // Update the store; without invalidation the cached value persists.
        await store.StoreAsync(CredKey, "second");
        Assert.Equal("first", await provider.GetAuthHeaderAsync(CancellationToken.None));

        provider.Invalidate();
        Assert.Equal("second", await provider.GetAuthHeaderAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StoreThrowing_FallsThroughToFallback()
    {
        var store = new ThrowingCredentialStore();

        var provider = new CredentialStoreAuthProvider(store, CredKey, fallback: "from-fallback");
        var token = await provider.GetAuthHeaderAsync(CancellationToken.None);

        Assert.Equal("from-fallback", token);
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
