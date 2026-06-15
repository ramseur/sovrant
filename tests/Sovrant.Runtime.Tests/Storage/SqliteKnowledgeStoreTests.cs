using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Knowledge;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

/// <summary>
/// Covers the Phase 109 copy-on-write overlay model: base (workspace_id='') rows are
/// shadowed by global ('global') and project (&lt;project-id&gt;) overlays without being
/// destroyed, and effective resolution honours project &gt; global &gt; base.
/// </summary>
public sealed class SqliteKnowledgeStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteKnowledgeStore _store;

    public SqliteKnowledgeStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_knowledge_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _store = new SqliteKnowledgeStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // A synthetic kind that the V035 seed never populates, so overlay tests start empty.
    private const string TestKind = "test-overlay";

    private static KnowledgePage Page(string slug, string tier, string workspaceId, string body, string? role = null) =>
        new(
            KnowledgeId: $"{tier}_{workspaceId}_{slug}",
            Kind: TestKind,
            Slug: slug,
            Name: slug,
            Description: $"{tier} {slug}",
            Tier: tier,
            Body: body,
            WorkspaceId: workspaceId,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Role: role);

    [Fact]
    public async Task GetEffective_GlobalOverlay_ShadowsBase_WithoutDestroyingIt()
    {
        await _provider.InitializeAsync();
        await _store.UpsertAsync(Page("code-review", "BuiltIn", KnowledgeScope.Base, "base body"));
        await _store.UpsertAsync(Page("code-review", "User", KnowledgeScope.Global, "user body"));

        var effective = await _store.GetEffectiveAsync(TestKind, "code-review");
        Assert.NotNull(effective);
        Assert.Equal("user body", effective!.Body);
        Assert.Equal("User", effective.Tier);

        // Base row still present and untouched.
        var baseRow = await _store.GetAsync(TestKind, "code-review", KnowledgeScope.Base);
        Assert.NotNull(baseRow);
        Assert.Equal("base body", baseRow!.Body);
    }

    [Fact]
    public async Task GetEffective_ProjectOverlay_WinsOverGlobalAndBase()
    {
        await _provider.InitializeAsync();
        await _store.UpsertAsync(Page("plan", "BuiltIn", KnowledgeScope.Base, "base body"));
        await _store.UpsertAsync(Page("plan", "User", KnowledgeScope.Global, "global body"));
        await _store.UpsertAsync(Page("plan", "User", "proj-123", "project body"));

        var effective = await _store.GetEffectiveAsync(TestKind, "plan", "proj-123");
        Assert.Equal("project body", effective!.Body);

        // A different project does not see proj-123's overlay; falls back to global.
        var other = await _store.GetEffectiveAsync(TestKind, "plan", "proj-999");
        Assert.Equal("global body", other!.Body);
    }

    [Fact]
    public async Task GetEffective_NoProject_FallsBackToBase()
    {
        await _provider.InitializeAsync();
        await _store.UpsertAsync(Page("tdd", "BuiltIn", KnowledgeScope.Base, "base body"));

        var effective = await _store.GetEffectiveAsync(TestKind, "tdd");
        Assert.Equal("base body", effective!.Body);
    }

    [Fact]
    public async Task RevertOverlay_RestoresBase()
    {
        await _provider.InitializeAsync();
        await _store.UpsertAsync(Page("refactor", "BuiltIn", KnowledgeScope.Base, "base body"));
        await _store.UpsertAsync(Page("refactor", "User", KnowledgeScope.Global, "edited body"));

        Assert.Equal("edited body", (await _store.GetEffectiveAsync(TestKind, "refactor"))!.Body);

        await _store.DeleteAsync(TestKind, "refactor", KnowledgeScope.Global);

        Assert.Equal("base body", (await _store.GetEffectiveAsync(TestKind, "refactor"))!.Body);
    }

    [Fact]
    public async Task GetAllEffective_MergesOverlaysShadowingBaseBySlug()
    {
        await _provider.InitializeAsync();
        // Two base skills; one is overridden globally.
        await _store.UpsertAsync(Page("alpha", "BuiltIn", KnowledgeScope.Base, "base alpha"));
        await _store.UpsertAsync(Page("beta", "BuiltIn", KnowledgeScope.Base, "base beta"));
        await _store.UpsertAsync(Page("alpha", "User", KnowledgeScope.Global, "user alpha"));
        // A user-created skill that has no base.
        await _store.UpsertAsync(Page("gamma", "User", KnowledgeScope.Global, "user gamma"));

        var all = await _store.GetAllEffectiveAsync(TestKind);
        Assert.Equal(3, all.Count); // alpha, beta, gamma — no duplicate alpha
        Assert.Equal("user alpha", all.Single(p => p.Slug == "alpha").Body);
        Assert.Equal("base beta", all.Single(p => p.Slug == "beta").Body);
        Assert.Equal("user gamma", all.Single(p => p.Slug == "gamma").Body);
    }

    [Fact]
    public async Task SeedMigration_PopulatesBuiltInSkillsAndAgents()
    {
        await _provider.InitializeAsync();

        var skills = await _store.GetAllAsync("skills", KnowledgeScope.Base);
        var agents = await _store.GetAllAsync("agents", KnowledgeScope.Base);

        Assert.Equal(32, skills.Count);
        Assert.Equal(25, agents.Count);
        Assert.All(skills, s => Assert.Equal("BuiltIn", s.Tier));
        Assert.All(agents, a => Assert.Equal("BuiltIn", a.Tier));

        // Spot-check a known skill carries its parsed metadata.
        var review = await _store.GetAsync("skills", "code-review", KnowledgeScope.Base);
        Assert.NotNull(review);
        Assert.Equal("/review", review!.Trigger);
        Assert.Contains("code-reviewer", review.Agents!, StringComparison.Ordinal);

        // Spot-check an agent carries role + recommended_level.
        var architect = await _store.GetAsync("agents", "architect", KnowledgeScope.Base);
        Assert.NotNull(architect);
        Assert.Equal("Planner", architect!.Role);
        Assert.Equal("High", architect.RecommendedLevel);
    }

    [Fact]
    public async Task RoleAndRecommendedLevel_RoundTrip()
    {
        await _provider.InitializeAsync();
        var page = new KnowledgePage(
            KnowledgeId: "agent_base_architect",
            Kind: "agents",
            Slug: "architect",
            Name: "architect",
            Description: "Planner agent",
            Tier: "BuiltIn",
            Body: "You are an architect.",
            WorkspaceId: KnowledgeScope.Base,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            Role: "Planner",
            RecommendedLevel: "High");
        await _store.UpsertAsync(page);

        var read = await _store.GetAsync("agents", "architect", KnowledgeScope.Base);
        Assert.Equal("Planner", read!.Role);
        Assert.Equal("High", read.RecommendedLevel);
    }
}
