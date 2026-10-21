using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Providers;

/// <summary>
/// Phase 145 Part B — members use the admin's configured providers: those enabled for a team
/// workspace, and the admin's default model set in every personal workspace. Real SQLite stores.
/// </summary>
public sealed class SharedModelAccessTests : IAsyncDisposable
{
    private const string Admin = "admin@example.com";
    private const string Sam = "sam@example.com";
    private const string Team = "ws-eng";
    private static readonly string SamPersonal = WorkspaceIdentity.DefaultPersonalFor(Sam);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sovrant_shared_{Guid.NewGuid():N}");
    private readonly SqliteStorageProvider _storage;
    private readonly SqliteUserPreferenceStore _prefs;
    private readonly SqliteProviderProfileStore _profiles;
    private readonly SqliteWorkspaceSettingsStore _ws;

    public SharedModelAccessTests()
    {
        Directory.CreateDirectory(_dir);
        _storage = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, Path.Combine(_dir, "sovrant.db"));
        _storage.InitializeAsync().GetAwaiter().GetResult();
        _prefs = new SqliteUserPreferenceStore(_storage);
        _profiles = new SqliteProviderProfileStore(_storage);
        _ws = new SqliteWorkspaceSettingsStore(_storage);
        // Admin → Providers creates profiles owned by the admin (not workspace-scoped).
        foreach (var (id, kind, model) in new[] { ("openai", "OpenAI", "gpt-4o-mini"), ("openrouter", "OpenRouter", "openai/gpt-4o"), ("ollama", "Ollama", "llama3.1:8b") })
            _profiles.CreateAsync(new ProviderProfile(id, Admin, kind, kind, "https://example.com/v1", model, 32000,
                $"provider.{id}.api_key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }

    private async Task<string[]> AllowedFor(string user, string ws) =>
        (await _profiles.ListUserAndWorkspaceAsync(user, ws, _ws)).Select(p => p.ProfileId).Order().ToArray();

    [Fact]
    public async Task Members_Can_Use_Admin_Providers_Enabled_For_Their_Team_Workspace()
    {
        await _ws.SetAsync(Team, WorkspaceSettingsKeys.EnabledProviderProfileIds, "openai,openrouter");
        Assert.Equal(["openai", "openrouter"], await AllowedFor(Sam, Team)); // before Part B: none (they weren't Sam's)
    }

    [Fact]
    public async Task The_Default_Set_Applies_To_Every_Personal_Workspace()
    {
        await _ws.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.PersonalDefaultProfileIds, "openai,ollama");
        Assert.Equal(["ollama", "openai"], await AllowedFor(Sam, SamPersonal));
        Assert.Equal(["ollama", "openai"], await AllowedFor("new@example.com", WorkspaceIdentity.DefaultPersonalFor("new@example.com")));

        // The admin changes it: takes effect for everyone at once.
        await _ws.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.PersonalDefaultProfileIds, "openrouter");
        Assert.Equal(["openrouter"], await AllowedFor(Sam, SamPersonal));
    }

    [Fact]
    public async Task Without_A_Default_Set_A_Personal_Workspace_Keeps_Its_Own_List()
    {
        await _ws.SetAsync(SamPersonal, WorkspaceSettingsKeys.EnabledProviderProfileIds, "openai");
        Assert.Equal(["openai"], await AllowedFor(Sam, SamPersonal)); // existing installs keep working
    }

    [Fact]
    public async Task A_Removed_Pick_Falls_Back_To_The_Default_Sets_Default()
    {
        await _ws.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.PersonalDefaultProfileIds, "openai,ollama");
        await _ws.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.PersonalDefaultProfileId, "ollama");
        await _prefs.SetAsync(Sam, UserPreferenceKeys.ActiveProviderProfileId, "openrouter"); // no longer in the set
        await _prefs.SetAsync(Sam, UserPreferenceKeys.Model, "openai/gpt-4o");

        var pick = await ModelSelection.ResolveAsync(Sam, SamPersonal, _prefs, _profiles, _ws);
        Assert.Equal(new ModelPick("ollama", "llama3.1:8b"), pick);
    }
}
