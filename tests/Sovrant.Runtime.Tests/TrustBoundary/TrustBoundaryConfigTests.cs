using System.Text.Json;
using Sovrant.Runtime.TrustBoundary;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.TrustBoundary;

public sealed class TrustBoundaryConfigTests
{
    [Fact]
    public void Resolve_NullStore_ReturnsSnapshotValues()
    {
        var snapshot = new TrustBoundaryConfig
        {
            Enabled = true,
            Sanitizer = new SanitizerConfig
            {
                Enabled = false,
                Mode = "warn",
                CorporateDomains = new[] { "acme.internal" },
                LogRedactions = true,
            },
            IntentVerification = new IntentVerificationConfig { Enabled = false },
        };

        var resolved = TrustBoundaryConfig.Resolve(snapshot, settings: null);

        Assert.True(resolved.Enabled);
        Assert.False(resolved.Sanitizer.Enabled);
        Assert.Equal("warn", resolved.Sanitizer.Mode);
        Assert.Equal(new[] { "acme.internal" }, resolved.Sanitizer.CorporateDomains);
        Assert.True(resolved.Sanitizer.LogRedactions);
        Assert.False(resolved.IntentVerification.Enabled);
    }

    [Fact]
    public void Resolve_StoreOverridesSnapshot()
    {
        var snapshot = new TrustBoundaryConfig
        {
            Enabled = false,
            Sanitizer = new SanitizerConfig { Mode = "redact" },
        };

        var store = new InMemoryStore();
        store.Seed(WorkspaceSettingsKeys.TrustBoundaryEnabled, "true");
        store.Seed(WorkspaceSettingsKeys.SanitizerMode, "block");
        store.Seed(WorkspaceSettingsKeys.SanitizerCorporateDomains, """["from-db.local"]""");
        store.Seed(WorkspaceSettingsKeys.SanitizerExemptProviders, """["ollama"]""");
        store.Seed(WorkspaceSettingsKeys.IntentBlockHarmful, "false");

        var resolved = TrustBoundaryConfig.Resolve(snapshot, store);

        Assert.True(resolved.Enabled);
        Assert.Equal("block", resolved.Sanitizer.Mode);
        Assert.Equal(new[] { "from-db.local" }, resolved.Sanitizer.CorporateDomains);
        Assert.Equal(new[] { "ollama" }, resolved.Sanitizer.ExemptProviders);
        Assert.False(resolved.IntentVerification.BlockHarmfulIntent);
    }

    [Fact]
    public void Resolve_CustomPatterns_RoundTripJsonFromStore()
    {
        var snapshot = new TrustBoundaryConfig();
        var patterns = new[] { new CustomPattern("internal-id", @"ACME-\d{6}", "block") };

        var store = new InMemoryStore();
        store.Seed(WorkspaceSettingsKeys.SanitizerCustomPatterns, JsonSerializer.Serialize(patterns));

        var resolved = TrustBoundaryConfig.Resolve(snapshot, store);

        var p = Assert.Single(resolved.Sanitizer.CustomPatterns);
        Assert.Equal("internal-id", p.Name);
        Assert.Equal(@"ACME-\d{6}", p.RegexPattern);
        Assert.Equal("block", p.Action);
    }

    [Fact]
    public async Task SaveToStoreAsync_PersistsAllFields()
    {
        var store = new InMemoryStore();
        var config = new TrustBoundaryConfig
        {
            Enabled = true,
            Sanitizer = new SanitizerConfig
            {
                Enabled = false,
                Mode = "warn",
                CorporateDomains = new[] { "acme.internal" },
                AllowList = new[] { "public.example.com" },
                ExemptProviders = new[] { "ollama" },
                CustomPatterns = new[] { new CustomPattern("id", @"\d+", "redact") },
                LogRedactions = true,
            },
            IntentVerification = new IntentVerificationConfig
            {
                Enabled = false,
                ClarifyAmbiguous = false,
                BlockHarmfulIntent = false,
            },
        };

        await config.SaveToStoreAsync(store);

        Assert.Equal("true", await store.GetGlobalAsync(WorkspaceSettingsKeys.TrustBoundaryEnabled));
        Assert.Equal("false", await store.GetGlobalAsync(WorkspaceSettingsKeys.SanitizerEnabled));
        Assert.Equal("warn", await store.GetGlobalAsync(WorkspaceSettingsKeys.SanitizerMode));
        Assert.Equal("true", await store.GetGlobalAsync(WorkspaceSettingsKeys.SanitizerLogRedactions));
        Assert.Equal("false", await store.GetGlobalAsync(WorkspaceSettingsKeys.IntentVerificationEnabled));

        var domains = JsonSerializer.Deserialize<List<string>>(
            (await store.GetGlobalAsync(WorkspaceSettingsKeys.SanitizerCorporateDomains))!);
        Assert.Equal(new[] { "acme.internal" }, domains);

        var allow = JsonSerializer.Deserialize<List<string>>(
            (await store.GetGlobalAsync(WorkspaceSettingsKeys.SanitizerAllowList))!);
        Assert.Equal(new[] { "public.example.com" }, allow);

        var patterns = JsonSerializer.Deserialize<List<CustomPattern>>(
            (await store.GetGlobalAsync(WorkspaceSettingsKeys.SanitizerCustomPatterns))!);
        var pat = Assert.Single(patterns!);
        Assert.Equal("id", pat.Name);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsAllFields()
    {
        var store = new InMemoryStore();
        var saved = new TrustBoundaryConfig
        {
            Enabled = true,
            Sanitizer = new SanitizerConfig
            {
                Mode = "block",
                CustomPatterns = new[] { new CustomPattern("ssn", @"\d{3}-\d{2}-\d{4}", "block") },
            },
        };

        await saved.SaveToStoreAsync(store);
        var loaded = TrustBoundaryConfig.Resolve(new TrustBoundaryConfig(), store);

        Assert.True(loaded.Enabled);
        Assert.Equal("block", loaded.Sanitizer.Mode);
        var pat = Assert.Single(loaded.Sanitizer.CustomPatterns);
        Assert.Equal("ssn", pat.Name);
    }

    [Fact]
    public async Task SaveToStoreAsync_InvalidMode_Throws()
    {
        var store = new InMemoryStore();
        var config = new TrustBoundaryConfig { Sanitizer = new SanitizerConfig { Mode = "destroy" } };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => config.SaveToStoreAsync(store));
        Assert.Contains("Mode", ex.Message);

        // Nothing should have been written.
        Assert.Null(await store.GetGlobalAsync(WorkspaceSettingsKeys.TrustBoundaryEnabled));
    }

    [Fact]
    public async Task SaveToStoreAsync_InvalidCustomRegex_Throws()
    {
        var store = new InMemoryStore();
        var config = new TrustBoundaryConfig
        {
            Sanitizer = new SanitizerConfig
            {
                CustomPatterns = new[] { new CustomPattern("bad", "[unterminated", "redact") },
            },
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => config.SaveToStoreAsync(store));
        Assert.Contains("bad", ex.Message);
    }

    [Fact]
    public void Validate_ValidConfig_DoesNotThrow()
    {
        var config = new TrustBoundaryConfig
        {
            Sanitizer = new SanitizerConfig
            {
                Mode = "redact",
                CustomPatterns = new[] { new CustomPattern("ok", @"\d+", "warn") },
            },
        };

        config.Validate();
    }

    private sealed class InMemoryStore : IWorkspaceSettingsStore
    {
        private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

        public void Seed(string key, string value) => _data[key] = value;

        public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_data.TryGetValue(key, out var v) ? v : null);

        public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default)
            => GetGlobalAsync(key, ct);

        public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default)
        {
            _data[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default)
        {
            _data.Remove(key);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(_data, StringComparer.Ordinal));
    }
}
