using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Ui;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Knowledge;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Onboarding;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.TrustBoundary;

namespace Sovrant.Runtime.Tests.Onboarding;

/// <summary>
/// Phase 140 — the Welcome page is role-aware and every checklist tick comes from real state.
/// Real SQLite for preferences and provider profiles; small fakes for agents and MCP servers.
/// </summary>
public sealed class OnboardingServiceTests : IAsyncDisposable
{
    private readonly string _baseDir;
    private readonly SqliteStorageProvider _storage;
    private readonly SqliteUserPreferenceStore _prefs;
    private readonly SqliteProviderProfileStore _profiles;
    private readonly FakeKnowledge _knowledge = new();
    private readonly FakeMcp _mcp = new();

    public OnboardingServiceTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), $"sovrant_onboarding_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDir);
        _storage = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, Path.Combine(_baseDir, "sovrant.db"));
        _storage.InitializeAsync().GetAwaiter().GetResult();
        _prefs = new SqliteUserPreferenceStore(_storage);
        _profiles = new SqliteProviderProfileStore(_storage);
    }

    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_baseDir, recursive: true); } catch { /* best-effort */ }
    }

    private OnboardingService Build(SovrantConfig? config = null, bool withPrefs = true)
    {
        var services = new ServiceCollection();
        if (withPrefs) services.AddSingleton<IUserPreferenceStore>(_prefs);
        services.AddSingleton<IProviderProfileStore>(_profiles);
        services.AddSingleton<IKnowledgeStore>(_knowledge);
        services.AddSingleton<IMcpServerStore>(_mcp);
        services.AddSingleton(config ?? new SovrantConfig());
        return new OnboardingService(services.BuildServiceProvider());
    }

    [Fact]
    public async Task Welcome_Is_Shown_Once_Per_User()
    {
        var onboarding = Build();
        Assert.False(await onboarding.HasSeenWelcomeAsync("alice"));

        await onboarding.MarkWelcomeSeenAsync("alice");

        Assert.True(await onboarding.HasSeenWelcomeAsync("alice"));
        Assert.False(await onboarding.HasSeenWelcomeAsync("bob"));
    }

    [Fact]
    public async Task Without_A_Preference_Store_The_Welcome_Is_Never_Forced()
    {
        Assert.True(await Build(withPrefs: false).HasSeenWelcomeAsync("alice"));
    }

    [Fact]
    public async Task Admin_Checklist_Starts_Empty_And_Ticks_From_Real_State()
    {
        var onboarding = Build();
        var empty = await onboarding.GetWelcomeAsync("admin@x", isAdmin: true, workspaceId: null);
        Assert.True(empty.IsAdmin);
        Assert.Equal(["provider", "workspace-providers", "invite", "integration", "agent"], empty.Checklist.Select(c => c.Key));
        Assert.Equal(0, empty.DoneCount);

        var now = DateTimeOffset.UtcNow;
        await _profiles.CreateAsync(new ProviderProfile("or", "admin@x", "OpenRouter", "OpenRouter", "https://openrouter.ai/api/v1", null, 32000, "c", now, now));
        _mcp.Servers["github"] = new McpServerConfig();
        _knowledge.Agents.Add(Agent("researcher", tier: "User"));

        var later = await onboarding.GetWelcomeAsync("admin@x", isAdmin: true, workspaceId: null);
        var byKey = later.Checklist.ToDictionary(c => c.Key);
        Assert.True(byKey["provider"].Done);
        Assert.Equal("OpenRouter connected", byKey["provider"].Subtitle);
        Assert.True(byKey["integration"].Done);
        Assert.True(byKey["agent"].Done);
        Assert.Equal(3, later.DoneCount);
    }

    [Fact]
    public async Task BuiltIn_Agents_Do_Not_Count_As_Your_First_Agent()
    {
        _knowledge.Agents.Add(Agent("architect", tier: "BuiltIn"));
        var welcome = await Build().GetWelcomeAsync("admin@x", isAdmin: true, workspaceId: null);
        Assert.False(welcome.Checklist.Single(c => c.Key == "agent").Done);
    }

    [Fact]
    public async Task Member_Checklist_Ticks_Model_And_Knowledge()
    {
        var onboarding = Build();
        await _prefs.SetAsync("sam", UserPreferenceKeys.Model, "gemma-4-31b-it");
        await _prefs.SetAsync("sam", UserPreferenceKeys.Provider, "OpenRouter");
        await _prefs.SetAsync("sam", UserPreferenceKeys.ActiveProviderProfileId, "or");
        await onboarding.MarkKnowledgeVisitedAsync("sam");
        _knowledge.Agents.Add(Agent("architect", tier: "BuiltIn"));

        var welcome = await onboarding.GetWelcomeAsync("sam", isAdmin: false, workspaceId: "ws");
        var byKey = welcome.Checklist.ToDictionary(c => c.Key);

        Assert.False(welcome.IsAdmin);
        Assert.Equal(["model", "conversation", "agent", "knowledge"], welcome.Checklist.Select(c => c.Key));
        Assert.True(byKey["model"].Done);
        Assert.Equal("gemma-4-31b-it via OpenRouter", byKey["model"].Subtitle);
        Assert.True(byKey["knowledge"].Done);
        Assert.Equal("Your workspace has 1 agent ready", byKey["agent"].Subtitle);
    }

    [Fact]
    public void Members_Never_Get_A_Link_Into_An_Admin_Page()
    {
        var admin = OnboardingService.Areas(isAdmin: true, trustBoundaryOn: false);
        var member = OnboardingService.Areas(isAdmin: false, trustBoundaryOn: false);

        Assert.Equal(8, admin.Count);
        Assert.Equal(8, member.Count);
        Assert.Contains(admin, a => a.Target == WelcomeTarget.Workspaces);
        Assert.All(member, a => Assert.False(a.Target is { } t && OnboardingService.IsAdminOnly(t), $"member area '{a.Title}' links to an admin page"));
        // admin-only areas are still described to members, just without a link
        var managed = member.Where(a => a.AdminManaged).ToList();
        Assert.Equal(["Integrations", "Privacy & governance"], managed.Select(a => a.Title));
        Assert.All(managed, a => { Assert.Null(a.Target); Assert.Null(a.ActionLabel); });
    }

    [Fact]
    public async Task Member_Checklist_Never_Targets_An_Admin_Page()
    {
        var welcome = await Build().GetWelcomeAsync("sam", isAdmin: false, workspaceId: null);
        Assert.All(welcome.Checklist, c => Assert.False(OnboardingService.IsAdminOnly(c.Target), c.Key));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Redaction_Is_Only_Promised_When_The_Trust_Boundary_Is_On(bool enabled, bool mentionsRedaction)
    {
        var config = new SovrantConfig { TrustBoundary = new TrustBoundaryConfig { Enabled = enabled } };
        var welcome = await Build(config).GetWelcomeAsync("sam", isAdmin: false, workspaceId: null);

        var privacy = welcome.Areas.Single(a => a.IconName == IconNames.Private);
        Assert.Equal(mentionsRedaction, privacy.Description.Contains("redacted", StringComparison.Ordinal));
    }

    private static KnowledgePage Agent(string slug, string tier) =>
        new($"k-{slug}", "agents", slug, slug, "", tier, "", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class FakeKnowledge : IKnowledgeStore
    {
        public List<KnowledgePage> Agents { get; } = [];
        public Task<IReadOnlyList<KnowledgePage>> GetAllAsync(string kind, string workspaceId = "", CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<KnowledgePage>>(kind == "agents" ? Agents.ToList() : []);
        public Task<KnowledgePage?> GetAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<KnowledgePage?> GetActiveAsync(string kind, string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<KnowledgePage?> GetEffectiveAsync(string kind, string slug, string projectId = "", CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<KnowledgePage>> GetAllEffectiveAsync(string kind, string projectId = "", CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertAsync(KnowledgePage page, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeMcp : IMcpServerStore
    {
        public Dictionary<string, McpServerConfig> Servers { get; } = new(StringComparer.Ordinal);
        public Task<IReadOnlyDictionary<string, McpServerConfig>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, McpServerConfig>>(Servers);
        public Task<IReadOnlyList<McpServerEntry>> GetAllEntriesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<McpServerConfig?> GetAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<McpServerEntry?> GetEntryAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpsertAsync(string name, McpServerConfig config, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
