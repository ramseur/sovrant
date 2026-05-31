using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Tools.Templates;

/// <summary>
/// In-memory index of user-authored tool templates, backed by <see cref="IKnowledgeStore"/> (Phase 112C).
/// Base rows live at <c>workspace_id=''</c>; user global edits at <c>workspace_id='global'</c>;
/// project-local templates at <c>workspace_id=KnowledgeScope.ProjectIdFor(cwd)</c>.
/// Higher-priority overlays shadow lower-priority rows by slug.
/// </summary>
public sealed partial class UserToolTemplateRegistry
{
    private readonly IKnowledgeStore _store;
    private readonly string _projectId;
    private readonly ILogger<UserToolTemplateRegistry> _logger;

    private Dictionary<string, UserToolTemplate> _bySlug;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load tool templates from DB: {Error}")]
    private static partial void LogLoadError(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {Count} tool templates from DB")]
    private static partial void LogLoaded(ILogger logger, int count);

    public UserToolTemplateRegistry(IKnowledgeStore store, ILogger<UserToolTemplateRegistry> logger)
    {
        _store = store;
        _logger = logger;
        _projectId = KnowledgeScope.ProjectIdFor(Directory.GetCurrentDirectory());
        _bySlug = LoadFromStore();
    }

    public IReadOnlyCollection<UserToolTemplate> All => _bySlug.Values;

    public UserToolTemplate? TryGet(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return _bySlug.GetValueOrDefault(slug);
    }

    /// <summary>
    /// Saves a tool template to the global user tier in the DB and refreshes the index.
    /// </summary>
    public void SaveGlobal(string slug, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(markdown);

        var parsed = UserToolTemplateParser.Parse(markdown, slug, UserToolTier.Global, "");
        if (parsed is null) return;

        var now = DateTimeOffset.UtcNow;
        var page = new KnowledgePage(
            KnowledgeId:  $"tool_global_{slug}",
            Kind:         "tools",
            Slug:         slug,
            Name:         parsed.Name,
            Description:  parsed.Description,
            Tier:         "User",
            Body:         parsed.Body,
            WorkspaceId:  KnowledgeScope.Global,
            CreatedAt:    now,
            UpdatedAt:    now,
            Category:     parsed.Category);

        _store.UpsertAsync(page).GetAwaiter().GetResult();
        Reload();
    }

    /// <summary>Deletes the global user-tier override for a slug and refreshes the index.</summary>
    public bool DeleteGlobal(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        _store.DeleteAsync("tools", slug, KnowledgeScope.Global).GetAwaiter().GetResult();
        Reload();
        return true;
    }

    /// <summary>Reloads the in-memory index from the DB.</summary>
    public void Reload() => _bySlug = LoadFromStore();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Dictionary<string, UserToolTemplate> LoadFromStore()
    {
        var bySlug = new Dictionary<string, UserToolTemplate>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<KnowledgePage> pages;
        try
        {
            pages = _store.GetAllEffectiveAsync("tools", _projectId).GetAwaiter().GetResult();
        }
        catch (SqliteException ex)
        {
            // knowledge_pages table not yet created — DB hasn't been migrated yet.
            LogLoadError(_logger, ex.Message);
            return bySlug;
        }

        foreach (var page in pages)
        {
            var tier = page.Tier == "BuiltIn" ? UserToolTier.BuiltIn
                : page.WorkspaceId.StartsWith("project:", StringComparison.Ordinal) ? UserToolTier.Project
                : UserToolTier.Global;

            bySlug[page.Slug] = new UserToolTemplate(
                page.Slug,
                page.Name,
                page.Description,
                page.Category ?? "general",
                page.Body,
                tier,
                SourcePath: "");
        }

        LogLoaded(_logger, bySlug.Count);
        return bySlug;
    }
}
