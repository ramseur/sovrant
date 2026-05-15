using System.Data.Common;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Workspaces;

/// <summary>
/// Tests for <see cref="WorkspaceSettingsResolver"/> — the env &gt; store &gt; fallback
/// chain shared by every Bucket-B consumer (executor, agent, compaction, budgets).
/// </summary>
public sealed class WorkspaceSettingsResolverTests : IDisposable
{
    private const string IntEnv = "SOVRANT_TEST_RES_INT";
    private const string DecEnv = "SOVRANT_TEST_RES_DEC";
    private const string StrEnv = "SOVRANT_TEST_RES_STR";

    private readonly string? _origInt = Environment.GetEnvironmentVariable(IntEnv);
    private readonly string? _origDec = Environment.GetEnvironmentVariable(DecEnv);
    private readonly string? _origStr = Environment.GetEnvironmentVariable(StrEnv);

    public WorkspaceSettingsResolverTests()
    {
        Environment.SetEnvironmentVariable(IntEnv, null);
        Environment.SetEnvironmentVariable(DecEnv, null);
        Environment.SetEnvironmentVariable(StrEnv, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(IntEnv, _origInt);
        Environment.SetEnvironmentVariable(DecEnv, _origDec);
        Environment.SetEnvironmentVariable(StrEnv, _origStr);
    }

    [Fact]
    public void ResolveInt_EnvBeatsStoreAndFallback()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "5");
        Environment.SetEnvironmentVariable(IntEnv, "9");

        Assert.Equal(9, WorkspaceSettingsResolver.ResolveInt(store, "k", IntEnv, fallback: 1));
    }

    [Fact]
    public void ResolveInt_StoreBeatsFallback()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "5");

        Assert.Equal(5, WorkspaceSettingsResolver.ResolveInt(store, "k", IntEnv, fallback: 1));
    }

    [Fact]
    public void ResolveInt_FallbackUsedWhenNothingElseSet()
        => Assert.Equal(7, WorkspaceSettingsResolver.ResolveInt(
            new InMemoryWorkspaceSettingsStore(), "k", IntEnv, fallback: 7));

    [Fact]
    public void ResolveInt_NullStore_StillReadsEnvAndFallback()
    {
        Environment.SetEnvironmentVariable(IntEnv, "42");
        Assert.Equal(42, WorkspaceSettingsResolver.ResolveInt(settings: null, "k", IntEnv, fallback: 1));

        Environment.SetEnvironmentVariable(IntEnv, null);
        Assert.Equal(1, WorkspaceSettingsResolver.ResolveInt(settings: null, "k", IntEnv, fallback: 1));
    }

    [Fact]
    public void ResolveInt_BadEnv_FallsThroughToStore()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "5");
        Environment.SetEnvironmentVariable(IntEnv, "not-a-number");

        Assert.Equal(5, WorkspaceSettingsResolver.ResolveInt(store, "k", IntEnv, fallback: 1));
    }

    [Fact]
    public void ResolveInt_BadStoreValue_FallsThroughToFallback()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "garbage");

        Assert.Equal(11, WorkspaceSettingsResolver.ResolveInt(store, "k", IntEnv, fallback: 11));
    }

    [Fact]
    public void ResolveInt_StoreThrowing_FallsThroughToFallback()
    {
        var store = new ThrowingWorkspaceSettingsStore();
        Assert.Equal(3, WorkspaceSettingsResolver.ResolveInt(store, "k", IntEnv, fallback: 3));
    }

    [Fact]
    public void ResolveInt_UsesInvariantCulture()
    {
        // Ensure parsing isn't culture-sensitive: a comma-separated number from
        // a German locale env var must NOT be interpreted as 1234.
        Environment.SetEnvironmentVariable(IntEnv, "1,234");
        var result = WorkspaceSettingsResolver.ResolveInt(
            new InMemoryWorkspaceSettingsStore(), "k", IntEnv, fallback: 99);
        Assert.Equal(99, result);
    }

    [Fact]
    public void ResolveDecimalOrNull_EnvBeatsStore()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "1.5");
        Environment.SetEnvironmentVariable(DecEnv, "2.75");

        Assert.Equal(2.75m, WorkspaceSettingsResolver.ResolveDecimalOrNull(store, "k", DecEnv));
    }

    [Fact]
    public void ResolveDecimalOrNull_StoreBeatsNothing()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "1.5");
        Assert.Equal(1.5m, WorkspaceSettingsResolver.ResolveDecimalOrNull(store, "k", DecEnv));
    }

    [Fact]
    public void ResolveDecimalOrNull_ReturnsNullWhenNothingResolves()
        => Assert.Null(WorkspaceSettingsResolver.ResolveDecimalOrNull(
            new InMemoryWorkspaceSettingsStore(), "k", DecEnv));

    [Fact]
    public void ResolveString_EnvBeatsStoreAndFallback()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "from-store");
        Environment.SetEnvironmentVariable(StrEnv, "from-env");

        Assert.Equal("from-env",
            WorkspaceSettingsResolver.ResolveString(store, "k", StrEnv, fallback: "from-fallback"));
    }

    [Fact]
    public void ResolveString_EmptyEnvTreatedAsUnset()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "from-store");
        Environment.SetEnvironmentVariable(StrEnv, string.Empty);

        Assert.Equal("from-store",
            WorkspaceSettingsResolver.ResolveString(store, "k", StrEnv, fallback: "from-fallback"));
    }

    [Fact]
    public void ResolveString_FallbackUsedWhenNothingElseSet()
        => Assert.Equal("from-fallback",
            WorkspaceSettingsResolver.ResolveString(
                new InMemoryWorkspaceSettingsStore(), "k", StrEnv, fallback: "from-fallback"));

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    public void ResolveBool_AcceptsCommonForms(string envValue, bool expected)
    {
        Environment.SetEnvironmentVariable(StrEnv, envValue);
        Assert.Equal(expected, WorkspaceSettingsResolver.ResolveBool(
            settings: null, "k", StrEnv, fallback: !expected));
    }

    [Fact]
    public void ResolveBool_EnvBeatsStoreAndFallback()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "false");
        Environment.SetEnvironmentVariable(StrEnv, "true");

        Assert.True(WorkspaceSettingsResolver.ResolveBool(store, "k", StrEnv, fallback: false));
    }

    [Fact]
    public void ResolveBool_StoreBeatsFallback()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "false");

        Assert.False(WorkspaceSettingsResolver.ResolveBool(store, "k", StrEnv, fallback: true));
    }

    [Fact]
    public void ResolveBool_BadValuesFallThrough()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "garbage");
        Environment.SetEnvironmentVariable(StrEnv, "also-garbage");

        Assert.True(WorkspaceSettingsResolver.ResolveBool(store, "k", StrEnv, fallback: true));
    }

    [Fact]
    public void ResolveStringList_EnvJsonBeatsStore()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", """["a","b"]""");
        Environment.SetEnvironmentVariable(StrEnv, """["x","y","z"]""");

        var result = WorkspaceSettingsResolver.ResolveStringList(
            store, "k", StrEnv, fallback: Array.Empty<string>());
        Assert.Equal(new[] { "x", "y", "z" }, result);
    }

    [Fact]
    public void ResolveStringList_EnvCommaSeparated()
    {
        Environment.SetEnvironmentVariable(StrEnv, "alpha, beta ,gamma");
        var result = WorkspaceSettingsResolver.ResolveStringList(
            settings: null, "k", StrEnv, fallback: Array.Empty<string>());
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, result);
    }

    [Fact]
    public void ResolveStringList_StoreJson()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", """["one","two"]""");
        var result = WorkspaceSettingsResolver.ResolveStringList(
            store, "k", StrEnv, fallback: Array.Empty<string>());
        Assert.Equal(new[] { "one", "two" }, result);
    }

    [Fact]
    public void ResolveStringList_FallbackUsedWhenNothingElseSet()
    {
        var fallback = new[] { "default" };
        var result = WorkspaceSettingsResolver.ResolveStringList(
            new InMemoryWorkspaceSettingsStore(), "k", StrEnv, fallback);
        Assert.Same(fallback, result);
    }

    [Fact]
    public void ResolveStringList_BadJsonInStore_FallsThrough()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "[not, valid, json");
        var fallback = new[] { "from-fallback" };
        var result = WorkspaceSettingsResolver.ResolveStringList(store, "k", StrEnv, fallback);
        Assert.Same(fallback, result);
    }

    [Fact]
    public void ResolveStringList_EmptyStringInStore_TreatedAsUnset()
    {
        var store = new InMemoryWorkspaceSettingsStore();
        store.Seed("k", "");
        var fallback = new[] { "kept" };
        var result = WorkspaceSettingsResolver.ResolveStringList(store, "k", StrEnv, fallback);
        Assert.Same(fallback, result);
    }

    private sealed class InMemoryWorkspaceSettingsStore : IWorkspaceSettingsStore
    {
        private readonly Dictionary<string, string> _global = new(StringComparer.Ordinal);

        public void Seed(string key, string value) => _global[key] = value;

        public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_global.TryGetValue(key, out var v) ? v : null);

        public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default)
            => GetGlobalAsync(key, ct);

        public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default)
        {
            _global[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default)
        {
            _global.Remove(key);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(_global, StringComparer.Ordinal));
    }

    private sealed class ThrowingWorkspaceSettingsStore : IWorkspaceSettingsStore
    {
        public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default)
            => throw new SimulatedDbFailure("simulated store failure");

        public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default)
            => throw new SimulatedDbFailure("simulated store failure");

        public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal));

        private sealed class SimulatedDbFailure : DbException
        {
            public SimulatedDbFailure(string message) : base(message) { }
        }
    }
}
