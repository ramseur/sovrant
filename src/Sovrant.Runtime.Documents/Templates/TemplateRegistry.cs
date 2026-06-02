using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// <see cref="ITemplateRegistry"/> backed by DI-injected C# templates and DB-backed
/// <see cref="DbDocumentTemplate"/> rows from <c>knowledge_pages</c> kind=<c>document-templates</c>
/// (Phase 112D). DB templates shadow same-Id C# templates so user overlays work.
/// </summary>
public sealed partial class TemplateRegistry : ITemplateRegistry
{
    private readonly Dictionary<string, IDocumentTemplate> _byId;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load document templates from DB: {Error}")]
    private static partial void LogLoadError(ILogger logger, string error);

    public TemplateRegistry(
        IEnumerable<IDocumentTemplate> csharpTemplates,
        IKnowledgeStore knowledgeStore,
        ILogger<TemplateRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(csharpTemplates);
        ArgumentNullException.ThrowIfNull(knowledgeStore);

        _byId = new Dictionary<string, IDocumentTemplate>(StringComparer.OrdinalIgnoreCase);

        // C# templates first (lowest priority)
        foreach (var t in csharpTemplates)
            _byId[t.Id] = t;

        // DB templates override (supports user overlays + base seeded templates)
        try
        {
            var pages = knowledgeStore.GetAllEffectiveAsync("document-templates")
                                      .GetAwaiter().GetResult();
            foreach (var page in pages)
            {
                var dbt = new DbDocumentTemplate(page);
                _byId[dbt.Id] = dbt;
            }
        }
        catch (SqliteException ex)
        {
            // DB not yet migrated — fall back to C# templates only
            LogLoadError(logger, ex.Message);
        }
    }

    public IDocumentTemplate Resolve(string id)
    {
        if (_byId.TryGetValue(id, out var template))
            return template;
        throw new KeyNotFoundException($"No template registered with ID '{id}'.");
    }

    public bool TryResolve(string id, out IDocumentTemplate? template) =>
        _byId.TryGetValue(id, out template);

    public IReadOnlyCollection<IDocumentTemplate> All => _byId.Values;

    public IEnumerable<IDocumentTemplate> Find(string? industry = null, string? search = null)
    {
        foreach (var template in _byId.Values)
        {
            if (!string.IsNullOrWhiteSpace(industry) &&
                !string.Equals(template.Industry, industry, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var needle = search.Trim();
                if (template.Id.Contains(needle, StringComparison.OrdinalIgnoreCase)) { yield return template; continue; }
                if (template.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)) { yield return template; continue; }
                if (template.Description.Contains(needle, StringComparison.OrdinalIgnoreCase)) { yield return template; continue; }
                continue;
            }

            yield return template;
        }
    }
}
