using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class LegacyConfigMigratorTests : IAsyncDisposable
{
    private readonly string _baseDir;
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _storage;
    private readonly SqliteUserPreferenceStore _prefs;
    private readonly SqliteProviderProfileStore _profiles;
    private readonly SqliteWorkspaceSettingsStore _wsSettings;
    private readonly AesGcmCredentialStore _credentials;
    private readonly LegacyConfigMigrator _migrator;

    public LegacyConfigMigratorTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), $"sovrant_migrator_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(Path.Combine(_baseDir, "credentials"));

        _dbPath = Path.Combine(_baseDir, "sovrant.db");
        _storage = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _storage.InitializeAsync().GetAwaiter().GetResult();

        _prefs = new SqliteUserPreferenceStore(_storage);
        _profiles = new SqliteProviderProfileStore(_storage);
        _wsSettings = new SqliteWorkspaceSettingsStore(_storage);
        _credentials = new AesGcmCredentialStore(Path.Combine(_baseDir, "credentials"));

        _migrator = new LegacyConfigMigrator(
            _prefs, _profiles, _credentials, _wsSettings,
            NullLogger<LegacyConfigMigrator>.Instance,
            baseDirectory: _baseDir);
    }

    public async ValueTask DisposeAsync()
    {
        _credentials.Dispose();
        await _storage.DisposeAsync();
        try { if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true); } catch { /* best-effort */ }
    }

    private void WriteFile(string name, string contents) =>
        File.WriteAllText(Path.Combine(_baseDir, name), contents);

    [Fact]
    public async Task RunAsync_NoFiles_IsNoOp()
    {
        var migrated = await _migrator.RunAsync("alice");
        Assert.Equal(0, migrated);
    }

    [Fact]
    public async Task SettingsJson_FieldsMoveToUserPreferences_AndApiKeyToCredentialStore()
    {
        WriteFile("settings.json", """
        {
            "Model": "gpt-5",
            "Provider": "OpenAI",
            "BaseUrl": "https://api.openai.com/v1",
            "MaxTokens": 8192,
            "PermissionMode": "BypassPermissions",
            "IntentRouting": false,
            "WebSearch": "Auto",
            "ApiKey": "sk-proj-secret-abc"
        }
        """);

        var migrated = await _migrator.RunAsync("alice");

        Assert.Equal(1, migrated);
        Assert.Equal("gpt-5", await _prefs.GetAsync("alice", UserPreferenceKeys.Model));
        Assert.Equal("OpenAI", await _prefs.GetAsync("alice", UserPreferenceKeys.Provider));
        Assert.Equal("https://api.openai.com/v1", await _prefs.GetAsync("alice", UserPreferenceKeys.BaseUrl));
        Assert.Equal("8192", await _prefs.GetAsync("alice", UserPreferenceKeys.MaxTokens));
        Assert.Equal("BypassPermissions", await _prefs.GetAsync("alice", UserPreferenceKeys.PermissionMode));
        Assert.Equal("false", await _prefs.GetAsync("alice", UserPreferenceKeys.IntentRouting));
        Assert.Equal("Auto", await _prefs.GetAsync("alice", UserPreferenceKeys.WebSearch));

        Assert.Equal("sk-proj-secret-abc",
            await _credentials.RetrieveAsync(Sovrant.Api.Auth.CredentialKeys.LlmApiKey));

        Assert.False(File.Exists(Path.Combine(_baseDir, "settings.json")));
        Assert.True(File.Exists(Path.Combine(_baseDir, "settings.json.bak")));
    }

    [Fact]
    public async Task ProvidersJson_EachEntryBecomesProfile_KeyEncrypted()
    {
        WriteFile("providers.json", """
        [
          {
            "Name": "OpenAI Default",
            "Provider": "OpenAI",
            "Model": "gpt-5",
            "BaseUrl": "https://api.openai.com/v1",
            "ApiKey": "sk-proj-aaa",
            "MaxTokens": 8192
          },
          {
            "Name": "OpenRouter",
            "Provider": "OpenRouter",
            "Model": "openrouter/auto",
            "BaseUrl": "https://openrouter.ai/api/v1",
            "ApiKey": "sk-or-v1-bbb",
            "MaxTokens": 16000
          }
        ]
        """);

        await _migrator.RunAsync("alice");

        var profiles = await _profiles.ListAsync("alice");
        Assert.Equal(2, profiles.Count);

        var openAi = profiles.Single(p => p.ProviderKind == "OpenAI");
        Assert.Equal("OpenAI Default", openAi.Name);
        Assert.Equal("gpt-5", openAi.DefaultModel);
        Assert.Equal(8192, openAi.MaxTokens);
        Assert.Equal("sk-proj-aaa", await _credentials.RetrieveAsync(openAi.CredentialId));

        var or = profiles.Single(p => p.ProviderKind == "OpenRouter");
        Assert.Equal("sk-or-v1-bbb", await _credentials.RetrieveAsync(or.CredentialId));

        Assert.True(File.Exists(Path.Combine(_baseDir, "providers.json.bak")));
    }

    [Fact]
    public async Task GovernanceJson_FieldsMoveToWorkspaceSettings()
    {
        WriteFile("governance.json", """
        {
            "governance_level": "strict",
            "audit_log": false,
            "blocked_commands": ["rm -rf /"],
            "protected_files": [".env"],
            "secret_patterns": ["sk-[A-Za-z0-9]{20,}"]
        }
        """);

        await _migrator.RunAsync("alice");

        Assert.Equal("strict", await _wsSettings.GetGlobalAsync(
            Sovrant.Runtime.Workspaces.WorkspaceSettingsKeys.GovernanceLevel));
        Assert.Equal("false", await _wsSettings.GetGlobalAsync(
            Sovrant.Runtime.Workspaces.WorkspaceSettingsKeys.GovernanceAuditLog));
        Assert.Equal("[\"rm -rf /\"]", await _wsSettings.GetGlobalAsync(
            Sovrant.Runtime.Workspaces.WorkspaceSettingsKeys.GovernanceBlockedCommands));
        Assert.True(File.Exists(Path.Combine(_baseDir, "governance.json.bak")));
    }

    [Fact]
    public async Task SecondRun_IsIdempotent()
    {
        WriteFile("settings.json", """{ "Model": "gpt-5", "ApiKey": "sk-proj-x" }""");

        var first = await _migrator.RunAsync("alice");
        var second = await _migrator.RunAsync("alice");

        Assert.Equal(1, first);
        Assert.Equal(0, second); // .bak rename means the source file no longer exists
        Assert.Equal("gpt-5", await _prefs.GetAsync("alice", UserPreferenceKeys.Model));
    }

    [Fact]
    public async Task ExistingPrefs_AreNotOverwritten()
    {
        // Simulate: user already saved a Model preference through the new
        // Settings UI before pulling the migrator. The legacy file lingers
        // from before. The DB value is the source of truth — don't clobber.
        await _prefs.SetAsync("alice", UserPreferenceKeys.Model, "claude-opus-4-7");

        WriteFile("settings.json", """{ "Model": "gpt-5" }""");
        await _migrator.RunAsync("alice");

        Assert.Equal("claude-opus-4-7", await _prefs.GetAsync("alice", UserPreferenceKeys.Model));
        Assert.True(File.Exists(Path.Combine(_baseDir, "settings.json.bak")));
    }

    [Fact]
    public async Task MalformedJson_LeavesFileInPlace_AndReturnsZero()
    {
        WriteFile("settings.json", "{ this is not json");

        var migrated = await _migrator.RunAsync("alice");

        Assert.Equal(0, migrated);
        Assert.True(File.Exists(Path.Combine(_baseDir, "settings.json")));
        Assert.False(File.Exists(Path.Combine(_baseDir, "settings.json.bak")));
    }

    [Fact]
    public async Task ProfileId_DerivedDeterministically_FromProviderAndName()
    {
        // Same input → same id → second run becomes an UPDATE not a duplicate INSERT.
        WriteFile("providers.json", """
        [{ "Name": "Default", "Provider": "OpenAI", "ApiKey": "k1", "BaseUrl": "u1" }]
        """);
        await _migrator.RunAsync("alice");

        // Recreate the file (user retained .bak but re-saved old name).
        WriteFile("providers.json", """
        [{ "Name": "Default", "Provider": "OpenAI", "ApiKey": "k2", "BaseUrl": "u1-updated" }]
        """);
        await _migrator.RunAsync("alice");

        var profiles = await _profiles.ListAsync("alice");
        Assert.Single(profiles);
        Assert.Equal("u1-updated", profiles[0].BaseUrl);
        Assert.Equal("k2", await _credentials.RetrieveAsync(profiles[0].CredentialId));
    }
}
