using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Config;

/// <summary>
/// Phase 144 — provider keys from the environment: imported on first boot, kept when an admin has
/// changed them (unless SOVRANT_ENV_KEYS_OVERRIDE=true), and a user with no provider gets a working,
/// one shared admin profile for LLM_API_KEY (Phase 145 Part B). Real SQLite stores; env values come from a dictionary.
/// </summary>
public sealed class EnvCredentialSeederTests : IAsyncDisposable
{
    private readonly string _baseDir;
    private readonly SqliteStorageProvider _storage;
    private readonly MemoryCredentials _credentials = new();
    private readonly SqliteUserPreferenceStore _prefs;
    private readonly SqliteProviderProfileStore _profiles;
    private readonly SqliteWorkspaceStore _workspaces;
    private readonly SqliteWorkspaceSettingsStore _wsSettings;

    public EnvCredentialSeederTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), $"sovrant_envseed_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDir);
        _storage = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, Path.Combine(_baseDir, "sovrant.db"));
        _storage.InitializeAsync().GetAwaiter().GetResult();
        _prefs = new SqliteUserPreferenceStore(_storage);
        _profiles = new SqliteProviderProfileStore(_storage);
        _workspaces = new SqliteWorkspaceStore(_storage);
        _wsSettings = new SqliteWorkspaceSettingsStore(_storage);
    }

    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_baseDir, recursive: true); } catch { /* best-effort */ }
    }

    private static Func<string, string?> Env(params (string Key, string Value)[] vars)
    {
        var map = vars.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
        return k => map.TryGetValue(k, out var v) ? v : null;
    }

    [Fact]
    public async Task First_Boot_Imports_Every_Env_Key_Into_The_Store()
    {
        var imported = await EnvCredentialSeeder.SeedAsync(_credentials, Env(
            ("LLM_API_KEY", "sk-llm"), ("OPENROUTER_API_KEY", "sk-or-x"), ("BRAVE_API_KEY", "brave-k"), ("FIRECRAWL_API_KEY", "fc-k")));

        Assert.Equal(["LLM_API_KEY", "OPENROUTER_API_KEY", "BRAVE_API_KEY", "FIRECRAWL_API_KEY"], imported);
        Assert.Equal("sk-llm", await _credentials.RetrieveAsync(CredentialKeys.LlmApiKey));
        Assert.Equal("sk-or-x", await _credentials.RetrieveAsync(CredentialKeys.OpenRouterApiKey));
        Assert.Equal("brave-k", await _credentials.RetrieveAsync(CredentialKeys.BraveApiKey));
        Assert.Equal("fc-k", await _credentials.RetrieveAsync(CredentialKeys.FirecrawlApiKey));
        Assert.Equal("sk-llm", await _credentials.RetrieveAsync(EnvCredentialSeeder.EnvProfileCredentialId));
    }

    [Fact]
    public async Task Openai_Alias_And_Native_Provider_Key_Work()
    {
        await EnvCredentialSeeder.SeedAsync(_credentials, Env(("OPENAI_API_KEY", "sk-alias"), ("PROVIDER_API_KEY", "sk-native")));
        Assert.Equal("sk-alias", await _credentials.RetrieveAsync(CredentialKeys.LlmApiKey));
        // PROVIDER_API_KEY is the native messages-API provider's key (PROVIDER_BASE_URL), not an LLM alias.
        Assert.Equal("sk-native", await _credentials.RetrieveAsync(CredentialKeys.ProviderApiKey));
    }

    [Fact]
    public async Task Admin_Changes_Survive_Restarts_Unless_Override_Is_On()
    {
        await _credentials.StoreAsync(CredentialKeys.LlmApiKey, "sk-set-by-admin");

        var imported = await EnvCredentialSeeder.SeedAsync(_credentials, Env(("LLM_API_KEY", "sk-from-env")));
        Assert.Empty(imported);
        Assert.Equal("sk-set-by-admin", await _credentials.RetrieveAsync(CredentialKeys.LlmApiKey));

        imported = await EnvCredentialSeeder.SeedAsync(_credentials, Env(("LLM_API_KEY", "sk-from-env"), (EnvCredentialSeeder.OverrideVariable, "true")));
        Assert.Equal(["LLM_API_KEY"], imported);
        Assert.Equal("sk-from-env", await _credentials.RetrieveAsync(CredentialKeys.LlmApiKey));
    }

    [Fact]
    public void Base_Url_And_Model_Become_The_Defaults()
    {
        var config = new SovrantConfig();
        EnvCredentialSeeder.ApplyDefaults(config, Env(("LLM_API_KEY", "sk-or-v1-abc"), ("SOVRANT_MODEL", "openai/gpt-4.1-mini")));
        Assert.Equal(new Uri("https://openrouter.ai/api/v1"), config.BaseUrl); // inferred from the key
        Assert.Equal("openai/gpt-4.1-mini", config.Model);

        var custom = new SovrantConfig();
        EnvCredentialSeeder.ApplyDefaults(custom, Env(("LLM_API_KEY", "x"), ("LLM_BASE_URL", "http://127.0.0.1:1234/v1")));
        Assert.Equal(new Uri("http://127.0.0.1:1234/v1"), custom.BaseUrl);
        Assert.Equal("LM Studio", EnvCredentialSeeder.KindFor(custom.BaseUrl!));
    }

    [Fact]
    public async Task The_First_Admin_Gets_One_Shared_Profile_That_Becomes_The_Default_Set()
    {
        var env = Env(("LLM_API_KEY", "sk-or-v1-abc"), ("SOVRANT_MODEL", "openai/gpt-4.1-mini"));
        await EnvCredentialSeeder.SeedAsync(_credentials, env);
        await CreateUserAsync("admin@example.com", "admin");
        await CreateUserAsync("sam@example.com", "user");

        await EnvCredentialSeeder.EnsureSharedProfileAsync(Services(), env);
        await EnvCredentialSeeder.EnsureSharedProfileAsync(Services(), env); // idempotent

        var shared = await _profiles.GetAsync(EnvCredentialSeeder.SharedProfileId);
        Assert.NotNull(shared);
        Assert.Equal("admin@example.com", shared!.UserId); // shows under Admin → Providers
        Assert.Equal("OpenRouter", shared.ProviderKind);
        Assert.Equal("openai/gpt-4.1-mini", shared.DefaultModel);
        Assert.Equal(EnvCredentialSeeder.EnvProfileCredentialId, shared.CredentialId);
        Assert.Empty(await _profiles.ListAsync("sam@example.com")); // no per-user profiles any more

        // Members use it in their personal workspace through the default set.
        var samPersonal = WorkspaceIdentity.DefaultPersonalFor("sam@example.com");
        var visible = await _profiles.ListUserAndWorkspaceAsync("sam@example.com", samPersonal, _wsSettings);
        Assert.Contains(visible, p => p.ProfileId == EnvCredentialSeeder.SharedProfileId);
    }

    [Fact]
    public async Task A_Default_Set_The_Admin_Chose_Is_Never_Overwritten()
    {
        await CreateUserAsync("admin@example.com", "admin");
        await _wsSettings.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.PersonalDefaultProfileIds, "admins-choice");

        await EnvCredentialSeeder.EnsureSharedProfileAsync(Services(), Env(("LLM_API_KEY", "sk-x")));

        Assert.Equal("admins-choice", await _wsSettings.GetGlobalAsync(WorkspaceSettingsKeys.PersonalDefaultProfileIds));
        Assert.NotNull(await _profiles.GetAsync(EnvCredentialSeeder.SharedProfileId)); // still available to enable
    }

    [Fact]
    public async Task Nothing_Happens_Without_A_Key_Or_Before_An_Admin_Exists()
    {
        await EnvCredentialSeeder.EnsureSharedProfileAsync(Services(), Env(("LLM_API_KEY", "sk-x"))); // no admin yet
        Assert.Null(await _profiles.GetAsync(EnvCredentialSeeder.SharedProfileId));

        await CreateUserAsync("admin@example.com", "admin");
        await EnvCredentialSeeder.EnsureSharedProfileAsync(Services(), Env()); // no key
        Assert.Null(await _profiles.GetAsync(EnvCredentialSeeder.SharedProfileId));
    }

    private async Task CreateUserAsync(string userId, string role)
    {
        await new Sovrant.Runtime.Users.SqliteUserStore(_storage, NullLogger<Sovrant.Runtime.Users.SqliteUserStore>.Instance).CreateAsync(userId: userId, role: role);
        await _workspaces.CreatePersonalWorkspaceAsync(userId);
    }

    private ServiceProvider Services() => new ServiceCollection()
        .AddSingleton<IUserPreferenceStore>(_prefs)
        .AddSingleton<IProviderProfileStore>(_profiles)
        .AddSingleton<IWorkspaceService>(_workspaces)
        .AddSingleton<IWorkspaceSettingsStore>(_wsSettings)
        .AddSingleton<ICredentialStore>(_credentials)
        .AddSingleton<Sovrant.Runtime.Users.IUserService>(new Sovrant.Runtime.Users.SqliteUserStore(_storage, NullLogger<Sovrant.Runtime.Users.SqliteUserStore>.Instance))
        .BuildServiceProvider();

    private sealed class MemoryCredentials : ICredentialStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
        public Task StoreAsync(string key, string value, CancellationToken ct = default) { _values[key] = value; return Task.CompletedTask; }
        public Task<string?> RetrieveAsync(string key, CancellationToken ct = default) => Task.FromResult(_values.TryGetValue(key, out var v) ? v : null);
        public Task DeleteAsync(string key, CancellationToken ct = default) { _values.Remove(key); return Task.CompletedTask; }
    }
}
