using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// In-memory index of user-authored document templates, backed by <see cref="IKnowledgeStore"/> (Phase 112C).
/// Base rows live at <c>workspace_id=''</c>; user global edits at <c>workspace_id='global'</c>;
/// project-local templates at <c>workspace_id=KnowledgeScope.ProjectIdFor(cwd)</c>.
/// Higher-priority overlays shadow lower-priority rows by slug.
/// </summary>
public sealed partial class UserDocumentTemplateRegistry
{
    private readonly IKnowledgeStore _store;
    private readonly string _projectId;
    private readonly ILogger<UserDocumentTemplateRegistry> _logger;

    private Dictionary<string, UserDocumentTemplate> _bySlug;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load document templates from DB: {Error}")]
    private static partial void LogLoadError(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {Count} document templates from DB")]
    private static partial void LogLoaded(ILogger logger, int count);

    public UserDocumentTemplateRegistry(IKnowledgeStore store, ILogger<UserDocumentTemplateRegistry> logger)
    {
        _store = store;
        _logger = logger;
        _projectId = KnowledgeScope.ProjectIdFor(Directory.GetCurrentDirectory());
        _bySlug = LoadFromStore();
    }

    /// <summary>All templates currently registered.</summary>
    public IReadOnlyCollection<UserDocumentTemplate> All => _bySlug.Values;

    /// <summary>Returns the template with the given slug (case-insensitive), or null.</summary>
    public UserDocumentTemplate? TryGet(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return _bySlug.GetValueOrDefault(slug);
    }

    /// <summary>Saves a markdown body to the global tier in the DB and refreshes the registry.</summary>
    public void SaveGlobal(string slug, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(markdown);

        var parsed = UserDocumentTemplateParser.Parse(markdown, slug, UserTemplateTier.Global, "");
        if (parsed is null) return;

        var now = DateTimeOffset.UtcNow;
        var page = new KnowledgePage(
            KnowledgeId:   $"doc_global_{slug}",
            Kind:          "documents",
            Slug:          slug,
            Name:          parsed.Name,
            Description:   parsed.Description,
            Tier:          "User",
            Body:          parsed.Body,
            WorkspaceId:   KnowledgeScope.Global,
            CreatedAt:     now,
            UpdatedAt:     now,
            Industry:      parsed.Industry,
            DefaultFormat: parsed.DefaultFormat);

        _store.UpsertAsync(page).GetAwaiter().GetResult();
        Reload();
    }

    /// <summary>Deletes the global user-tier override for a slug and refreshes the index.</summary>
    public bool DeleteGlobal(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        _store.DeleteAsync("documents", slug, KnowledgeScope.Global).GetAwaiter().GetResult();
        Reload();
        return true;
    }

    /// <summary>Reloads the in-memory index from the DB.</summary>
    public void Reload() => _bySlug = LoadFromStore();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Dictionary<string, UserDocumentTemplate> LoadFromStore()
    {
        var bySlug = new Dictionary<string, UserDocumentTemplate>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<KnowledgePage> pages;
        try
        {
            pages = _store.GetAllEffectiveAsync("documents", _projectId).GetAwaiter().GetResult();
        }
        catch (SqliteException ex)
        {
            // knowledge_pages table not yet created — DB hasn't been migrated yet.
            LogLoadError(_logger, ex.Message);
            return bySlug;
        }

        foreach (var page in pages)
        {
            var tier = page.Tier == "BuiltIn" ? UserTemplateTier.BuiltIn
                : page.WorkspaceId.StartsWith("project:", StringComparison.Ordinal) ? UserTemplateTier.Project
                : UserTemplateTier.Global;

            bySlug[page.Slug] = new UserDocumentTemplate(
                page.Slug,
                page.Name,
                page.Description,
                page.Industry ?? "general",
                page.DefaultFormat ?? "markdown",
                page.Body,
                tier,
                SourcePath: "");
        }

        LogLoaded(_logger, bySlug.Count);
        return bySlug;
    }
}
