using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Knowledge;
using Sovrant.Tools.Skills;

namespace Sovrant.Tools.Tests.Skills;

public sealed class SkillRegistryTests
{
    private static FakeSkillStore MakeStore(params KnowledgePage[] pages)
    {
        var store = new FakeSkillStore();
        foreach (var p in pages) store.Seed(p);
        return store;
    }

    private static KnowledgePage SkillPage(string slug, string name, string trigger, string body,
        string tier = "BuiltIn", string workspaceId = "")
    {
        var now = DateTimeOffset.UtcNow;
        return new KnowledgePage($"skill_{workspaceId}_{slug}", "skills", slug, name, "",
            tier, body, workspaceId, now, now, Trigger: trigger);
    }

    private static SkillRegistry CreateRegistry(FakeSkillStore store) =>
        new(store, NullLogger<SkillRegistry>.Instance);

    [Fact]
    public void All_ReturnsSeededSkills()
    {
        var store = MakeStore(SkillPage("tdd-workflow", "tdd-workflow", "/tdd", "TDD body"));
        var registry = CreateRegistry(store);
        Assert.Single(registry.All);
    }

    [Fact]
    public void TryGetByName_ReturnsSkill()
    {
        var store = MakeStore(SkillPage("tdd-workflow", "tdd-workflow", "/tdd", "TDD body"));
        var registry = CreateRegistry(store);
        var skill = registry.TryGetByName("tdd-workflow");
        Assert.NotNull(skill);
        Assert.Equal("tdd-workflow", skill.Name);
        Assert.Equal("/tdd", skill.Trigger);
    }

    [Fact]
    public void TryGetByTrigger_ReturnsSkill()
    {
        var store = MakeStore(SkillPage("tdd-workflow", "tdd-workflow", "/tdd", "TDD body"));
        var registry = CreateRegistry(store);
        var skill = registry.TryGetByTrigger("/tdd");
        Assert.NotNull(skill);
        Assert.Equal("tdd-workflow", skill.Name);
    }

    [Fact]
    public void TryGetByName_NotFound_ReturnsNull()
    {
        var registry = CreateRegistry(new FakeSkillStore());
        Assert.Null(registry.TryGetByName("nonexistent-skill"));
    }

    [Fact]
    public void TryGetByTrigger_NotFound_ReturnsNull()
    {
        var registry = CreateRegistry(new FakeSkillStore());
        Assert.Null(registry.TryGetByTrigger("/nonexistent"));
    }

    [Fact]
    public void Register_AddsSkill()
    {
        var registry = CreateRegistry(new FakeSkillStore());
        var skill = new SkillDefinition("dynamic", "Dynamic skill", "/dynamic", [], [], "Do stuff");

        registry.Register(skill);

        Assert.NotNull(registry.TryGetByName("dynamic"));
        Assert.NotNull(registry.TryGetByTrigger("/dynamic"));
    }

    [Fact]
    public void SaveGlobal_WritesToDbAndReloads()
    {
        var store = new FakeSkillStore();
        var registry = CreateRegistry(store);

        registry.SaveGlobal("my-skill", """
            ---
            name: my-skill
            description: A custom skill
            trigger: /mine
            ---
            Custom workflow body
            """);

        var skill = registry.TryGetByName("my-skill");
        Assert.NotNull(skill);
        Assert.Equal("/mine", skill.Trigger);
        Assert.Contains("Custom workflow body", skill.Body);
    }

    [Fact]
    public void DeleteGlobal_RemovesUserOverlay_BaseReEmerges()
    {
        var store = MakeStore(
            SkillPage("code-review", "code-review", "/review", "base body", "BuiltIn", ""),
            SkillPage("code-review", "code-review", "/review", "override body", "User", "global"));

        var registry = CreateRegistry(store);
        Assert.Equal("override body", registry.TryGetByName("code-review")?.Body);

        registry.DeleteGlobal("code-review");

        Assert.NotNull(registry.TryGetByName("code-review"));
        Assert.Equal("base body", registry.TryGetByName("code-review")?.Body);
    }

    [Fact]
    public void Reload_RefreshesFromStore()
    {
        var store = new FakeSkillStore();
        var registry = CreateRegistry(store);
        Assert.Empty(registry.All);

        store.Seed(SkillPage("new-skill", "new-skill", "/new", "body", "User", "global"));
        registry.Reload();

        Assert.Single(registry.All);
        Assert.NotNull(registry.TryGetByName("new-skill"));
    }
}

/// <summary>In-memory <see cref="IKnowledgeStore"/> for skill unit tests.</summary>
internal sealed class FakeSkillStore : IKnowledgeStore
{
    private readonly List<KnowledgePage> _pages = [];

    public void Seed(KnowledgePage page) => _pages.Add(page);

    public Task<IReadOnlyList<KnowledgePage>> GetAllAsync(string kind, string workspaceId = "", CancellationToken ct = default)
    {
        IReadOnlyList<KnowledgePage> r = _pages.Where(p => p.Kind == kind && p.WorkspaceId == workspaceId).ToList();
        return Task.FromResult(r);
    }

    public Task<KnowledgePage?> GetAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default) =>
        Task.FromResult(_pages.FirstOrDefault(p => p.Kind == kind && p.Slug == slug && p.WorkspaceId == workspaceId));

    public Task<KnowledgePage?> GetActiveAsync(string kind, string slug, CancellationToken ct = default) =>
        Task.FromResult(_pages.Where(p => p.Kind == kind && p.Slug == slug)
            .OrderByDescending(p => p.Tier == "User" ? 1 : 0).FirstOrDefault());

    public Task<KnowledgePage?> GetEffectiveAsync(string kind, string slug, string projectId = "", CancellationToken ct = default)
    {
        var c = _pages.Where(p => p.Kind == kind && p.Slug == slug).ToList();
        if (!string.IsNullOrEmpty(projectId))
        {
            var proj = c.FirstOrDefault(p => p.WorkspaceId == projectId);
            if (proj is not null) return Task.FromResult<KnowledgePage?>(proj);
        }
        var global = c.FirstOrDefault(p => p.WorkspaceId == "global");
        if (global is not null) return Task.FromResult<KnowledgePage?>(global);
        return Task.FromResult(c.FirstOrDefault(p => p.WorkspaceId == ""));
    }

    public Task<IReadOnlyList<KnowledgePage>> GetAllEffectiveAsync(string kind, string projectId = "", CancellationToken ct = default)
    {
        var all = _pages.Where(p => p.Kind == kind).ToList();
        var r = new Dictionary<string, KnowledgePage>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in all.Where(p => p.WorkspaceId == "")) r[p.Slug] = p;
        foreach (var p in all.Where(p => p.WorkspaceId == "global")) r[p.Slug] = p;
        if (!string.IsNullOrEmpty(projectId))
            foreach (var p in all.Where(p => p.WorkspaceId == projectId)) r[p.Slug] = p;
        return Task.FromResult<IReadOnlyList<KnowledgePage>>(r.Values.ToList());
    }

    public Task UpsertAsync(KnowledgePage page, CancellationToken ct = default)
    {
        var idx = _pages.FindIndex(p => p.Kind == page.Kind && p.Slug == page.Slug && p.WorkspaceId == page.WorkspaceId);
        if (idx >= 0) _pages[idx] = page;
        else _pages.Add(page);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default)
    {
        _pages.RemoveAll(p => p.Kind == kind && p.Slug == slug && p.WorkspaceId == workspaceId);
        return Task.CompletedTask;
    }
}
