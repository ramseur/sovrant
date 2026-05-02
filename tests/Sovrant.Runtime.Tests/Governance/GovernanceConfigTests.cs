using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class GovernanceConfigTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-gov-cfg-{Guid.NewGuid():N}");

    public GovernanceConfigTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_DbValuesOverrideJsonFiles()
    {
        var dir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "governance.json"), """
        {
            "governance_level": "permissive",
            "blocked_commands": ["from-json"],
            "audit_log": false
        }
        """);

        var store = new InMemoryStore();
        store.Seed(WorkspaceSettingsKeys.GovernanceLevel, "strict");
        store.Seed(WorkspaceSettingsKeys.GovernanceAuditLog, "true");
        store.Seed(WorkspaceSettingsKeys.GovernanceBlockedCommands, """["from-db-1","from-db-2"]""");

        var config = GovernanceConfig.Load(_tempDir, store);

        Assert.Equal(GovernanceLevel.Strict, config.Level);
        Assert.True(config.AuditLog);
        Assert.Equal(2, config.BlockedCommands.Count);
        Assert.Contains("from-db-1", config.BlockedCommands);
        Assert.Contains("from-db-2", config.BlockedCommands);
    }

    [Fact]
    public void Load_DbMissing_FallsBackToJsonFiles()
    {
        var dir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "governance.json"), """
        {
            "governance_level": "strict",
            "blocked_commands": ["from-json"]
        }
        """);

        var store = new InMemoryStore();
        var config = GovernanceConfig.Load(_tempDir, store);

        Assert.Equal(GovernanceLevel.Strict, config.Level);
        Assert.Single(config.BlockedCommands);
        Assert.Contains("from-json", config.BlockedCommands);
    }

    [Fact]
    public void Load_NullStore_BehavesLikeLegacyLoad()
    {
        var dir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "governance.json"), """
        {
            "governance_level": "minimal"
        }
        """);

        var config = GovernanceConfig.Load(_tempDir, settings: null);
        Assert.Equal(GovernanceLevel.Minimal, config.Level);
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
        var loaded = GovernanceConfig.Load(_tempDir, store);

        Assert.Equal(GovernanceLevel.Minimal, loaded.Level);
        Assert.False(loaded.AuditLog);
        Assert.Equal("dangerous-cmd", Assert.Single(loaded.BlockedCommands));
        Assert.Equal("*.key", Assert.Single(loaded.ProtectedFiles));
        Assert.Equal("AWS_[A-Z]+", Assert.Single(loaded.SecretPatterns));
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
    public void Load_NoFile_ReturnsDefaults()
    {
        var config = GovernanceConfig.Load(_tempDir);

        Assert.Equal(GovernanceLevel.Standard, config.Level);
        Assert.True(config.AuditLog);
        Assert.Empty(config.BlockedCommands);
        Assert.Empty(config.ProtectedFiles);
        Assert.Empty(config.SecretPatterns);
    }

    [Fact]
    public void Load_ValidJson_ParsesAllFields()
    {
        var dir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "governance.json"), """
        {
            "governance_level": "strict",
            "blocked_commands": ["rm -rf /home"],
            "protected_files": ["*.lock"],
            "secret_patterns": ["MY_SECRET_[0-9]+"],
            "audit_log": false
        }
        """);

        var config = GovernanceConfig.Load(_tempDir);

        Assert.Equal(GovernanceLevel.Strict, config.Level);
        Assert.False(config.AuditLog);
        Assert.Single(config.BlockedCommands);
        Assert.Contains("rm -rf /home", config.BlockedCommands);
        Assert.Single(config.ProtectedFiles);
        Assert.Single(config.SecretPatterns);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaults()
    {
        var dir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "governance.json"), "not json{{{");

        var config = GovernanceConfig.Load(_tempDir);
        Assert.Equal(GovernanceLevel.Standard, config.Level);
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
}
