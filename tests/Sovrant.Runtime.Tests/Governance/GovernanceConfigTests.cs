using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class GovernanceConfigTests
{
    [Fact]
    public void Load_DbValuesPopulateConfig()
    {
        var store = new InMemoryStore();
        store.Seed(WorkspaceSettingsKeys.GovernanceLevel, "strict");
        store.Seed(WorkspaceSettingsKeys.GovernanceAuditLog, "true");
        store.Seed(WorkspaceSettingsKeys.GovernanceBlockedCommands, """["from-db-1","from-db-2"]""");

        var config = GovernanceConfig.Load(store);

        Assert.Equal(GovernanceLevel.Strict, config.Level);
        Assert.True(config.AuditLog);
        Assert.Equal(2, config.BlockedCommands.Count);
        Assert.Contains("from-db-1", config.BlockedCommands);
        Assert.Contains("from-db-2", config.BlockedCommands);
    }

    [Fact]
    public void Load_NullStore_ReturnsDefaults()
    {
        var config = GovernanceConfig.Load(settings: null);

        Assert.Equal(GovernanceLevel.Standard, config.Level);
        Assert.True(config.AuditLog);
        Assert.Empty(config.BlockedCommands);
        Assert.Empty(config.ProtectedFiles);
        Assert.Empty(config.SecretPatterns);
    }

    [Fact]
    public void Load_EmptyStore_ReturnsDefaults()
    {
        var config = GovernanceConfig.Load(new InMemoryStore());

        Assert.Equal(GovernanceLevel.Standard, config.Level);
        Assert.True(config.AuditLog);
        Assert.Empty(config.BlockedCommands);
    }

    [Fact]
    public async Task SaveToStoreAsync_PersistsAllFiveFields()
    {
        var store = new InMemoryStore();
        var config = new GovernanceConfig { GovernanceLevelName = "strict", AuditLog = false };
        config.BlockedCommands.Add("rm -rf /");
        config.ProtectedFiles.Add(".env");
        config.SecretPatterns.Add("sk-[a-z0-9]+");

        await config.SaveToStoreAsync(store);

        Assert.Equal("strict", await store.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceLevel));
        Assert.Equal("false", await store.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceAuditLog));

        var blocked = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
            (await store.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceBlockedCommands))!);
        Assert.Equal(new[] { "rm -rf /" }, blocked);

        var files = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
            (await store.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceProtectedFiles))!);
        Assert.Equal(new[] { ".env" }, files);

        var patterns = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
            (await store.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceSecretPatterns))!);
        Assert.Equal(new[] { "sk-[a-z0-9]+" }, patterns);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsAllFields()
    {
        var store = new InMemoryStore();
        var saved = new GovernanceConfig { GovernanceLevelName = "minimal", AuditLog = false };
        saved.BlockedCommands.Add("dangerous-cmd");
        saved.ProtectedFiles.Add("*.key");
        saved.SecretPatterns.Add("AWS_[A-Z]+");

        await saved.SaveToStoreAsync(store);
        var loaded = GovernanceConfig.Load(store);

        Assert.Equal(GovernanceLevel.Minimal, loaded.Level);
        Assert.False(loaded.AuditLog);
        Assert.Equal("dangerous-cmd", Assert.Single(loaded.BlockedCommands));
        Assert.Equal("*.key", Assert.Single(loaded.ProtectedFiles));
        Assert.Equal("AWS_[A-Z]+", Assert.Single(loaded.SecretPatterns));
    }

    [Fact]
    public async Task SaveToStoreAsync_InvalidLevel_Throws()
    {
        var store = new InMemoryStore();
        var config = new GovernanceConfig { GovernanceLevelName = "ultra" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => config.SaveToStoreAsync(store));
        Assert.Contains("GovernanceLevelName", ex.Message);
        Assert.Null(await store.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceLevel));
    }

    [Fact]
    public async Task SaveToStoreAsync_InvalidSecretRegex_Throws()
    {
        var store = new InMemoryStore();
        var config = new GovernanceConfig { GovernanceLevelName = "standard" };
        config.SecretPatterns.Add("[unterminated");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => config.SaveToStoreAsync(store));
        Assert.Contains("[unterminated", ex.Message);
    }

    [Fact]
    public void Level_InvalidName_DefaultsToStandard()
    {
        var config = new GovernanceConfig { GovernanceLevelName = "invalid" };
        Assert.Equal(GovernanceLevel.Standard, config.Level);
    }

    [Fact]
    public void Level_CaseInsensitive()
    {
        var config = new GovernanceConfig { GovernanceLevelName = "MINIMAL" };
        Assert.Equal(GovernanceLevel.Minimal, config.Level);
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
