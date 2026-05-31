using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Knowledge;
using Sovrant.Tools.Templates;

namespace Sovrant.Tools.Tests.Templates;

public sealed class UserToolTemplateRegistryTests
{
    private static UserToolTemplateRegistry CreateRegistry(IKnowledgeStore store) =>
        new(store, NullLogger<UserToolTemplateRegistry>.Instance);

    [Fact]
    public void All_ReturnsRowsFromStore()
    {
        var store = new FakeKnowledgeStore();
        store.Seed(MakePage("code-review", "Code Review Guide", "Quality", "quality", "BuiltIn", ""));

        var registry = CreateRegistry(store);

        var template = registry.TryGet("code-review");
        Assert.NotNull(template);
        Assert.Equal("Code Review Guide", template.Name);
        Assert.Equal("quality", template.Category);
        Assert.Equal(UserToolTier.BuiltIn, template.Tier);
        Assert.Equal("Quality", template.Body);
    }

    [Fact]
    public void SaveGlobal_WritesToDbAndReloads()
    {
        var store = new FakeKnowledgeStore();
        var registry = CreateRegistry(store);

        registry.SaveGlobal("my-tool", """
            ---
            name: My Tool Guide
            description: A guide for my tool
            category: devops
            ---
            Use this tool when you want to automate deployments.
            """);

        var template = registry.TryGet("my-tool");
        Assert.NotNull(template);
        Assert.Equal("My Tool Guide", template.Name);
        Assert.Equal("devops", template.Category);
        Assert.Equal(UserToolTier.Global, template.Tier);
        Assert.Contains("automate deployments", template.Body);
    }

    [Fact]
    public void DeleteGlobal_RemovesUserOverlay_BaseRowReEmerges()
    {
        var store = new FakeKnowledgeStore();
        store.Seed(MakePage("shared", "Base", "base body", "general", "BuiltIn", ""));
        store.Seed(MakePage("shared", "Override", "override body", "general", "User", "global"));

        var registry = CreateRegistry(store);
        Assert.Equal("Override", registry.TryGet("shared")?.Name);

        registry.DeleteGlobal("shared");

        Assert.Equal("Base", registry.TryGet("shared")?.Name);
        Assert.Equal(UserToolTier.BuiltIn, registry.TryGet("shared")?.Tier);
    }

    [Fact]
    public void TryGet_UnknownSlug_ReturnsNull()
    {
        var registry = CreateRegistry(new FakeKnowledgeStore());
        Assert.Null(registry.TryGet("does-not-exist"));
    }

    [Fact]
    public void Reload_RefreshesFromStore()
    {
        var store = new FakeKnowledgeStore();
        var registry = CreateRegistry(store);
        Assert.Empty(registry.All);

        store.Seed(MakePage("new-tool", "New Tool", "body", "general", "User", "global"));
        registry.Reload();

        Assert.Single(registry.All);
        Assert.NotNull(registry.TryGet("new-tool"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static KnowledgePage MakePage(string slug, string name, string body, string category, string tier, string workspaceId)
    {
        var now = DateTimeOffset.UtcNow;
        return new KnowledgePage(
            KnowledgeId: $"tool_{workspaceId}_{slug}",
            Kind: "tools",
            Slug: slug,
            Name: name,
            Description: "",
            Tier: tier,
            Body: body,
            WorkspaceId: workspaceId,
            CreatedAt: now,
            UpdatedAt: now,
            Category: category);
    }
}

/// <summary>
/// Minimal in-memory <see cref="IKnowledgeStore"/> for unit tests.
/// Implements the same overlay resolution logic as <c>SqliteKnowledgeStore</c>.
/// </summary>
internal sealed class FakeKnowledgeStore : IKnowledgeStore
{
    private readonly List<KnowledgePage> _pages = [];

    public void Seed(KnowledgePage page) => _pages.Add(page);

    public Task<IReadOnlyList<KnowledgePage>> GetAllAsync(string kind, string workspaceId = "", CancellationToken ct = default)
    {
        IReadOnlyList<KnowledgePage> result = _pages.Where(p => p.Kind == kind && p.WorkspaceId == workspaceId).ToList();
        return Task.FromResult(result);
    }

    public Task<KnowledgePage?> GetAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default)
    {
        var page = _pages.FirstOrDefault(p => p.Kind == kind && p.Slug == slug && p.WorkspaceId == workspaceId);
        return Task.FromResult(page);
    }

    public Task<KnowledgePage?> GetActiveAsync(string kind, string slug, CancellationToken ct = default)
    {
        var page = _pages
            .Where(p => p.Kind == kind && p.Slug == slug)
            .OrderByDescending(p => p.Tier == "User" ? 1 : 0)
            .FirstOrDefault();
        return Task.FromResult(page);
    }

    public Task<KnowledgePage?> GetEffectiveAsync(string kind, string slug, string projectId = "", CancellationToken ct = default)
    {
        var candidates = _pages.Where(p => p.Kind == kind && p.Slug == slug).ToList();
        if (!string.IsNullOrEmpty(projectId))
        {
            var proj = candidates.FirstOrDefault(p => p.WorkspaceId == projectId);
            if (proj is not null) return Task.FromResult<KnowledgePage?>(proj);
        }
        var global = candidates.FirstOrDefault(p => p.WorkspaceId == "global");
        if (global is not null) return Task.FromResult<KnowledgePage?>(global);
        return Task.FromResult(candidates.FirstOrDefault(p => p.WorkspaceId == ""));
    }

    public Task<IReadOnlyList<KnowledgePage>> GetAllEffectiveAsync(string kind, string projectId = "", CancellationToken ct = default)
    {
        var all = _pages.Where(p => p.Kind == kind).ToList();
        var result = new Dictionary<string, KnowledgePage>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in all.Where(p => p.WorkspaceId == ""))
            result[p.Slug] = p;
        foreach (var p in all.Where(p => p.WorkspaceId == "global"))
            result[p.Slug] = p;
        if (!string.IsNullOrEmpty(projectId))
            foreach (var p in all.Where(p => p.WorkspaceId == projectId))
                result[p.Slug] = p;
        return Task.FromResult<IReadOnlyList<KnowledgePage>>(result.Values.ToList());
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
