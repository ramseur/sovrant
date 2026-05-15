using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Api.Config;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Config;

/// <summary>
/// Phase 88-C — covers the post-init step that pulls a user's saved
/// preferences and active-profile credential into the singleton
/// <see cref="SovrantConfig"/>. This is the boot-path read that makes the
/// migrator's writes visible on every subsequent boot once the legacy JSON
/// files have been renamed to *.bak.
/// </summary>
public sealed class ApplyUserPreferencesAsyncTests : IAsyncDisposable
{
    private readonly string _baseDir;
    private readonly SqliteStorageProvider _storage;
    private readonly AesGcmCredentialStore _credentials;
    private readonly SqliteUserPreferenceStore _prefs;
    private readonly SqliteProviderProfileStore _profiles;

    public ApplyUserPreferencesAsyncTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), $"sovrant_apply_prefs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(Path.Combine(_baseDir, "credentials"));

        _storage = new SqliteStorageProvider(
            NullLogger<SqliteStorageProvider>.Instance,
            Path.Combine(_baseDir, "sovrant.db"));
        _storage.InitializeAsync().GetAwaiter().GetResult();

        _credentials = new AesGcmCredentialStore(Path.Combine(_baseDir, "credentials"));
        _prefs = new SqliteUserPreferenceStore(_storage);
        _profiles = new SqliteProviderProfileStore(_storage);
    }

    public async ValueTask DisposeAsync()
    {
        _credentials.Dispose();
        await _storage.DisposeAsync();
        try { if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true); }
        catch { /* best-effort */ }
    }

    private IServiceProvider BuildServices(SovrantConfig config)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUserPreferenceStore>(_prefs);
        services.AddSingleton<IProviderProfileStore>(_profiles);
        services.AddSingleton<ICredentialStore>(_credentials);
        services.AddSingleton(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ScalarPreferences_AreCopiedIntoSovrantConfig()
    {
        await _prefs.SetAsync("alice", UserPreferenceKeys.Model, "gpt-5");
        await _prefs.SetAsync("alice", UserPreferenceKeys.MaxTokens, "16000");
        await _prefs.SetAsync("alice", UserPreferenceKeys.BaseUrl, "https://api.openai.com/v1");
        await _prefs.SetAsync("alice", UserPreferenceKeys.PermissionMode, "BypassPermissions");
        await _prefs.SetAsync("alice", UserPreferenceKeys.WebSearch, "Brave");

        var config = new SovrantConfig();
        var sp = BuildServices(config);

        await ServiceCollectionExtensions.ApplyUserPreferencesAsync(sp, "alice", default);

        Assert.Equal("gpt-5", config.Model);
        Assert.Equal(16000, config.MaxTokens);
        Assert.Equal(new Uri("https://api.openai.com/v1"), config.BaseUrl);
        Assert.Equal(PermissionMode.BypassPermissions, config.PermissionMode);
        Assert.Equal(WebSearchBackend.Brave, config.WebSearchOverride);
    }

    [Fact]
    public async Task NoPreferences_LeavesConfigDefaultsUntouched()
    {
        var config = new SovrantConfig();
        var defaultModel = config.Model;
        var defaultMaxTokens = config.MaxTokens;
        var sp = BuildServices(config);

        await ServiceCollectionExtensions.ApplyUserPreferencesAsync(sp, "alice", default);

        Assert.Equal(defaultModel, config.Model);
        Assert.Equal(defaultMaxTokens, config.MaxTokens);
        Assert.Null(config.BaseUrl);
        Assert.Null(config.ApiKey);
        Assert.Null(config.WebSearchOverride);
    }

    [Fact]
    public async Task ActiveProfile_AppliesProfileFieldsAndCredential()
    {
        await _credentials.StoreAsync("provider.openai-default.api_key", "sk-proj-from-profile");

        var profile = new ProviderProfile(
            ProfileId: "openai-default",
            UserId: "alice",
            Name: "OpenAI",
            ProviderKind: "OpenAI",
            BaseUrl: "https://api.openai.com/v1",
            DefaultModel: "gpt-5",
            MaxTokens: 8192,
            CredentialId: "provider.openai-default.api_key",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);
        await _profiles.CreateAsync(profile);
        await _prefs.SetAsync("alice", UserPreferenceKeys.ActiveProviderProfileId, "openai-default");

        var config = new SovrantConfig();
        var sp = BuildServices(config);

        await ServiceCollectionExtensions.ApplyUserPreferencesAsync(sp, "alice", default);

        Assert.Equal("gpt-5", config.Model);
        Assert.Equal(8192, config.MaxTokens);
        Assert.Equal(new Uri("https://api.openai.com/v1"), config.BaseUrl);
        Assert.Equal("sk-proj-from-profile", config.ApiKey);
    }

    [Fact]
    public async Task NoActiveProfile_FallsBackToGlobalLlmApiKeyCredential()
    {
        await _credentials.StoreAsync(CredentialKeys.LlmApiKey, "sk-global-fallback");

        var config = new SovrantConfig();
        var sp = BuildServices(config);

        await ServiceCollectionExtensions.ApplyUserPreferencesAsync(sp, "alice", default);

        Assert.Equal("sk-global-fallback", config.ApiKey);
    }

    [Fact]
    public async Task ActiveProfileMissingCredential_FallsBackToGlobalKey()
    {
        await _credentials.StoreAsync(CredentialKeys.LlmApiKey, "sk-global-fallback");

        var profile = new ProviderProfile(
            ProfileId: "openai-default",
            UserId: "alice",
            Name: "OpenAI",
            ProviderKind: "OpenAI",
            BaseUrl: "https://api.openai.com/v1",
            DefaultModel: null,
            MaxTokens: null,
            CredentialId: "provider.openai-default.api_key",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);
        await _profiles.CreateAsync(profile);
        await _prefs.SetAsync("alice", UserPreferenceKeys.ActiveProviderProfileId, "openai-default");

        var config = new SovrantConfig();
        var sp = BuildServices(config);

        await ServiceCollectionExtensions.ApplyUserPreferencesAsync(sp, "alice", default);

        Assert.Equal("sk-global-fallback", config.ApiKey);
    }

    [Fact]
    public async Task MalformedScalarValues_AreIgnored()
    {
        await _prefs.SetAsync("alice", UserPreferenceKeys.MaxTokens, "not-a-number");
        await _prefs.SetAsync("alice", UserPreferenceKeys.BaseUrl, "not a url");
        await _prefs.SetAsync("alice", UserPreferenceKeys.PermissionMode, "Sudoer");
        await _prefs.SetAsync("alice", UserPreferenceKeys.WebSearch, "Quantum");

        var config = new SovrantConfig();
        var defaultMaxTokens = config.MaxTokens;
        var defaultPermissionMode = config.PermissionMode;
        var sp = BuildServices(config);

        await ServiceCollectionExtensions.ApplyUserPreferencesAsync(sp, "alice", default);

        Assert.Equal(defaultMaxTokens, config.MaxTokens);
        Assert.Null(config.BaseUrl);
        Assert.Equal(defaultPermissionMode, config.PermissionMode);
        Assert.Null(config.WebSearchOverride);
    }

    [Fact]
    public async Task ActiveProfilePointingToMissingId_FallsBackToGlobalKey()
    {
        await _credentials.StoreAsync(CredentialKeys.LlmApiKey, "sk-global-fallback");
        await _prefs.SetAsync("alice", UserPreferenceKeys.ActiveProviderProfileId, "ghost-profile");

        var config = new SovrantConfig();
        var sp = BuildServices(config);

        await ServiceCollectionExtensions.ApplyUserPreferencesAsync(sp, "alice", default);

        Assert.Equal("sk-global-fallback", config.ApiKey);
    }
}
