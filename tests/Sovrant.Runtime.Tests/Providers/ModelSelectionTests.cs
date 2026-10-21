using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Providers;

/// <summary>
/// Phase 145 Part B — which admin-configured model a member's conversation uses: their own pick when
/// it's allowed on the workspace, else the workspace default, else the first allowed profile.
/// Real SQLite stores.
/// </summary>
public sealed class ModelSelectionTests : IAsyncDisposable
{
    private const string Ws = "ws-eng";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sovrant_modelsel_{Guid.NewGuid():N}");
    private readonly SqliteStorageProvider _storage;
    private readonly SqliteUserPreferenceStore _prefs;
    private readonly SqliteProviderProfileStore _profiles;
    private readonly SqliteWorkspaceSettingsStore _ws;

    public ModelSelectionTests()
    {
        Directory.CreateDirectory(_dir);
        _storage = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, Path.Combine(_dir, "sovrant.db"));
        _storage.InitializeAsync().GetAwaiter().GetResult();
        _prefs = new SqliteUserPreferenceStore(_storage);
        _profiles = new SqliteProviderProfileStore(_storage);
        _ws = new SqliteWorkspaceSettingsStore(_storage);
        // Admin-configured, workspace-scoped profiles: OpenAI and Anthropic belong to this workspace,
        // OpenRouter to another one (so it isn't allowed here).
        foreach (var (id, kind, model, ws) in new[] { ("openai", "OpenAI", "gpt-4o-mini", Ws), ("anthropic", "Anthropic", "claude-sonnet", Ws), ("openrouter", "OpenRouter", "openai/gpt-4o", "ws-other") })
            _profiles.CreateAsync(new ProviderProfile(id, "admin@example.com", kind, kind, "https://example.com/v1", model, 32000,
                $"provider.{id}.api_key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, WorkspaceId: ws)).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
    }

    private Task<ModelPick?> Resolve(string user, string ws = Ws) => ModelSelection.ResolveAsync(user, ws, _prefs, _profiles, _ws);

    [Fact]
    public async Task A_Members_Own_Allowed_Pick_Is_Used()
    {
        await _prefs.SetAsync("sam@example.com", UserPreferenceKeys.ActiveProviderProfileId, "anthropic");
        await _prefs.SetAsync("sam@example.com", UserPreferenceKeys.Model, "claude-opus");
        await _prefs.SetAsync("kim@example.com", UserPreferenceKeys.ActiveProviderProfileId, "openai");
        await _prefs.SetAsync("kim@example.com", UserPreferenceKeys.Model, "gpt-4o");

        Assert.Equal(new ModelPick("anthropic", "claude-opus"), await Resolve("sam@example.com"));
        Assert.Equal(new ModelPick("openai", "gpt-4o"), await Resolve("kim@example.com")); // each their own
    }

    [Fact]
    public async Task A_Pick_That_Is_Not_Allowed_Here_Falls_Back_To_The_Workspace_Default()
    {
        await _prefs.SetAsync("sam@example.com", UserPreferenceKeys.ActiveProviderProfileId, "openrouter"); // not enabled here
        await _prefs.SetAsync("sam@example.com", UserPreferenceKeys.Model, "openai/gpt-4o");
        await _ws.SetAsync(Ws, WorkspaceSettingsKeys.ActiveProviderProfileId, "anthropic");

        Assert.Equal(new ModelPick("anthropic", "claude-sonnet"), await Resolve("sam@example.com"));
    }

    [Fact]
    public async Task With_No_Pick_And_No_Workspace_Default_The_First_Allowed_Profile_Is_Used()
    {
        var pick = await Resolve("new@example.com");
        Assert.NotNull(pick);
        Assert.Contains(pick!.ProfileId, new[] { "openai", "anthropic" });
    }

    [Fact]
    public async Task A_Workspace_That_Allows_Nothing_Uses_The_Install_Default()
    {
        Assert.Null(await Resolve("sam@example.com", ws: "ws-empty"));
    }

    // ── Work outside a chat uses its owner's pick, not the install default ──────────────────────

    [Fact]
    public async Task Background_Work_Gets_Its_Owners_Pick()
    {
        await _prefs.SetAsync("sam@example.com", UserPreferenceKeys.ActiveProviderProfileId, "anthropic");
        await _prefs.SetAsync("sam@example.com", UserPreferenceKeys.Model, "claude-opus");

        var context = await BackgroundModel.ContextForAsync("sam@example.com", Ws, "s-1", _prefs, _profiles, _ws);

        Assert.NotNull(context);
        Assert.Equal(("sam@example.com", "anthropic", "claude-opus", "s-1"),
            (context!.OwnerUserId, context.ProviderProfileId, context.Model, context.SessionId));
        Assert.Null(await BackgroundModel.ContextForAsync(null, Ws, null, _prefs, _profiles, _ws)); // no owner
    }

    [Fact]
    public async Task A_Workflow_Started_Outside_A_Chat_Runs_On_Its_Owners_Model()
    {
        await _prefs.SetAsync("sam@example.com", UserPreferenceKeys.ActiveProviderProfileId, "anthropic");
        await _prefs.SetAsync("sam@example.com", UserPreferenceKeys.Model, "claude-opus");
        var store = new Sovrant.Runtime.Workflows.SqliteWorkflowStore(_storage);
        var wf = await store.CreateAsync("goal", workspaceId: Ws, ownerUserId: "sam@example.com");
        var seen = new Recorder();
        var executor = new Sovrant.Runtime.Workflows.TimeLimitedWorkflowExecutor(seen, store, _ws, prefs: _prefs, profiles: _profiles);

        await executor.RunAsync(wf.Id);

        Assert.Equal(("claude-opus", "anthropic", "sam@example.com"), seen.Context);
    }

    [Fact]
    public async Task A_Workflow_Started_From_A_Chat_Keeps_That_Conversations_Model()
    {
        await _prefs.SetAsync("sam@example.com", UserPreferenceKeys.ActiveProviderProfileId, "anthropic");
        var store = new Sovrant.Runtime.Workflows.SqliteWorkflowStore(_storage);
        var wf = await store.CreateAsync("goal", workspaceId: Ws, ownerUserId: "sam@example.com");
        var seen = new Recorder();
        var executor = new Sovrant.Runtime.Workflows.TimeLimitedWorkflowExecutor(seen, store, _ws, prefs: _prefs, profiles: _profiles);

        using (Sovrant.Runtime.Conversation.SessionContext.Push(new Sovrant.Runtime.Conversation.SessionConfig
               { OwnerUserId = "sam@example.com", ProviderProfileId = "openai", Model = "gpt-4o" }))
            await executor.RunAsync(wf.Id);

        Assert.Equal(("gpt-4o", "openai", "sam@example.com"), seen.Context);
    }

    private sealed class Recorder : Sovrant.Runtime.Workflows.IWorkflowExecutor
    {
        public (string?, string?, string?) Context { get; private set; }
        public Task<Sovrant.Runtime.Workflows.Workflow> RunAsync(string workflowId, CancellationToken ct = default)
        {
            var c = Sovrant.Runtime.Conversation.SessionContext.Current;
            Context = (c?.Model, c?.ProviderProfileId, c?.OwnerUserId);
            return Task.FromResult<Sovrant.Runtime.Workflows.Workflow>(null!);
        }
    }
}
