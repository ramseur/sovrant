using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Agents.Templates;

/// <summary>
/// <see cref="ITemplateLoader"/> that reads agent templates from <see cref="IKnowledgeStore"/>
/// (Phase 112). Replaces the disk-scan tier; the DB overlay model provides the same
/// BuiltIn → Global → Project layering.
/// </summary>
internal sealed partial class DbAgentTemplateLoader : ITemplateLoader
{
    private readonly IKnowledgeStore _store;
    private readonly string _projectId;
    private readonly ILogger<DbAgentTemplateLoader> _logger;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load agent templates from DB: {Error}")]
    private static partial void LogLoadError(ILogger logger, string error);

    public DbAgentTemplateLoader(IKnowledgeStore store, ILogger<DbAgentTemplateLoader> logger)
    {
        _store = store;
        _logger = logger;
        _projectId = KnowledgeScope.ProjectIdFor(Directory.GetCurrentDirectory());
    }

    public IReadOnlyDictionary<string, AgentTemplate> Load()
    {
        IReadOnlyList<KnowledgePage> pages;
        try
        {
            pages = _store.GetAllEffectiveAsync("agents", _projectId).GetAwaiter().GetResult();
        }
        catch (SqliteException ex)
        {
            // knowledge_pages table not yet created — DB hasn't been migrated yet.
            LogLoadError(_logger, ex.Message);
            return new Dictionary<string, AgentTemplate>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, AgentTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
        {
            if (!Enum.TryParse<AgentRole>(page.Role, ignoreCase: true, out var role))
                role = AgentRole.General;
            if (!Enum.TryParse<RecommendedLevel>(page.RecommendedLevel, ignoreCase: true, out var level))
                level = RecommendedLevel.Standard;

            var allowedTools = DeserializeList(page.Tools);
            var isBuiltIn = page.Tier == "BuiltIn";
            var template = new AgentTemplate(
                page.Name,
                role,
                level,
                allowedTools,
                string.IsNullOrWhiteSpace(page.Body) ? $"You are a {page.Name} agent." : page.Body,
                isBuiltIn,
                string.IsNullOrWhiteSpace(page.Description) ? null : page.Description);

            result[template.Name] = template;
        }
        return result;
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
