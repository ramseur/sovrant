using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Tools.Skills;

/// <summary>
/// In-memory index of all skills, backed by <see cref="IKnowledgeStore"/> (Phase 112).
/// Built-in rows live at <c>workspace_id=''</c>; user global edits at <c>workspace_id='global'</c>;
/// project-local skills at <c>workspace_id=KnowledgeScope.ProjectIdFor(cwd)</c>.
/// Higher-priority overlays shadow lower-priority rows by slug.
/// </summary>
public sealed partial class SkillRegistry
{
    private readonly IKnowledgeStore _store;
    private readonly string _projectId;
    private readonly ILogger<SkillRegistry> _logger;

    private Dictionary<string, SkillDefinition> _byName;
    private Dictionary<string, SkillDefinition> _byTrigger;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load skills from DB: {Error}")]
    private static partial void LogLoadError(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {Count} skills from DB")]
    private static partial void LogLoaded(ILogger logger, int count);

    public SkillRegistry(IKnowledgeStore store, ILogger<SkillRegistry> logger)
    {
        _store = store;
        _logger = logger;
        _projectId = KnowledgeScope.ProjectIdFor(Directory.GetCurrentDirectory());
        (_byName, _byTrigger) = LoadFromStore();
    }

    /// <summary>All skills currently registered.</summary>
    public IReadOnlyCollection<SkillDefinition> All => _byName.Values;

    /// <summary>Returns the skill with the given name, or null if not found.</summary>
    public SkillDefinition? TryGetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _byName.GetValueOrDefault(name);
    }

    /// <summary>Returns the skill matching the given trigger (e.g. <c>/tdd</c>), or null.</summary>
    public SkillDefinition? TryGetByTrigger(string trigger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        return _byTrigger.GetValueOrDefault(trigger);
    }

    /// <summary>
    /// Saves a skill to the global user tier in the DB and refreshes the in-memory index.
    /// </summary>
    public void SaveGlobal(string slug, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(markdown);

        var skill = SkillParser.Parse(markdown);
        if (skill is null) return;

        var now = DateTimeOffset.UtcNow;
        var page = new KnowledgePage(
            KnowledgeId:   $"skill_global_{slug}",
            Kind:          "skills",
            Slug:          slug,
            Name:          skill.Name,
            Description:   skill.Description,
            Tier:          "User",
            Body:          skill.Body,
            WorkspaceId:   KnowledgeScope.Global,
            CreatedAt:     now,
            UpdatedAt:     now,
            Trigger:       string.IsNullOrEmpty(skill.Trigger) ? null : skill.Trigger,
            Agents:        skill.Agents.Count > 0 ? JsonSerializer.Serialize(skill.Agents) : null,
            Tools:         skill.Tools.Count > 0 ? JsonSerializer.Serialize(skill.Tools) : null);

        _store.UpsertAsync(page).GetAwaiter().GetResult();
        Reload();
    }

    /// <summary>Deletes the global user-tier override for a slug and refreshes the index.</summary>
    public bool DeleteGlobal(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        _store.DeleteAsync("skills", slug, KnowledgeScope.Global).GetAwaiter().GetResult();
        Reload();
        return true;
    }

    /// <summary>
    /// Path used by callers that still need a filesystem location for display purposes.
    /// Returns null for DB-backed skills (Phase 112+).
    /// </summary>
    public static SkillSource? TryGetSource(string name) => null;

    /// <summary>Registers a skill definition (used by SkillCreateTool for runtime creation).</summary>
    public void Register(SkillDefinition skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _byName[skill.Name] = skill;
        if (!string.IsNullOrWhiteSpace(skill.Trigger))
            _byTrigger[skill.Trigger] = skill;
    }

    /// <summary>Reloads the in-memory index from the DB.</summary>
    public void Reload() => (_byName, _byTrigger) = LoadFromStore();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private (Dictionary<string, SkillDefinition> byName, Dictionary<string, SkillDefinition> byTrigger) LoadFromStore()
    {
        var byName = new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);
        var byTrigger = new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<KnowledgePage> pages;
        try
        {
            pages = _store.GetAllEffectiveAsync("skills", _projectId).GetAwaiter().GetResult();
        }
        catch (SqliteException ex)
        {
            // knowledge_pages table not yet created — DB hasn't been migrated yet.
            LogLoadError(_logger, ex.Message);
            return (byName, byTrigger);
        }

        foreach (var page in pages)
        {
            var agents = DeserializeList(page.Agents);
            var tools = DeserializeList(page.Tools);
            var skill = new SkillDefinition(
                page.Name,
                page.Description,
                page.Trigger ?? string.Empty,
                agents,
                tools,
                string.IsNullOrWhiteSpace(page.Body) ? $"Execute the {page.Name} skill." : page.Body,
                page.Tier);

            byName[skill.Name] = skill;
            if (!string.IsNullOrWhiteSpace(skill.Trigger))
                byTrigger[skill.Trigger] = skill;
        }

        LogLoaded(_logger, byName.Count);
        return (byName, byTrigger);
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}

/// <summary>Origin of a registered skill (retained for API compatibility; always null for DB-backed skills).</summary>
public enum SkillTier { BuiltIn, Global, Project }

/// <summary>Where a registered skill came from. Phase 112: always null for DB-backed skills.</summary>
public sealed record SkillSource(string Path, SkillTier Tier);
