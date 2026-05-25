using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Projects.Templates;

/// <summary>
/// Phase 73 — Indexes registered <see cref="IProjectTemplate"/> implementations
/// and provides lookup by exact ID or by language + kind hint.
/// </summary>
/// <remarks>
/// Registered as a singleton. Implementations are injected via
/// <c>IEnumerable&lt;IProjectTemplate&gt;</c> — add scaffolds by calling
/// <c>services.AddSingleton&lt;IProjectTemplate, MyTemplate&gt;()</c>.
/// </remarks>
public sealed partial class ProjectTemplateRegistry
{
    private readonly IReadOnlyDictionary<string, IProjectTemplate> _byId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<IProjectTemplate>> _byLanguage;

    [LoggerMessage(Level = LogLevel.Debug, Message = "ProjectTemplateRegistry: loaded {Count} template(s)")]
    private static partial void LogLoaded(ILogger logger, int count);

    public ProjectTemplateRegistry(
        IEnumerable<IProjectTemplate> templates,
        ILogger<ProjectTemplateRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(templates);

        var list = templates.ToList();

        _byId = list.ToDictionary(
            t => t.Id,
            t => t,
            StringComparer.OrdinalIgnoreCase);

        _byLanguage = list
            .GroupBy(t => t.Language, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<IProjectTemplate>)g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        LogLoaded(logger, list.Count);
    }

    /// <summary>All registered templates.</summary>
    public IReadOnlyCollection<IProjectTemplate> All => (IReadOnlyCollection<IProjectTemplate>)_byId.Values;

    /// <summary>All distinct language keys present in the registry.</summary>
    public IReadOnlyCollection<string> Languages => (IReadOnlyCollection<string>)_byLanguage.Keys;

    /// <summary>
    /// Returns the template with the given <c>{language}/{kind}</c> ID, or
    /// <see langword="null"/> if not registered.
    /// </summary>
    public IProjectTemplate? TryGet(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.GetValueOrDefault(id);
    }

    /// <summary>
    /// Finds the best-matching template for a language and optional kind hint.
    /// <list type="bullet">
    ///   <item>Exact ID match (<c>node/express-api</c>) wins first.</item>
    ///   <item>Then kind substring match within the language group.</item>
    ///   <item>Falls back to the first template registered for that language.</item>
    /// </list>
    /// Returns <see langword="null"/> if no template is registered for the language.
    /// </summary>
    public IProjectTemplate? Find(string language, string? kind = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        // Try exact ID if caller passed "language/kind" as the language arg
        if (language.Contains('/', StringComparison.Ordinal) && _byId.TryGetValue(language, out var byExact))
            return byExact;

        if (!_byLanguage.TryGetValue(language, out var group))
            return null;

        if (string.IsNullOrWhiteSpace(kind))
            return group[0];

        // Prefer exact kind match, then substring, then first in group
        return group.FirstOrDefault(t => t.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            ?? group.FirstOrDefault(t => t.Kind.Contains(kind, StringComparison.OrdinalIgnoreCase))
            ?? group[0];
    }

    /// <summary>Returns all templates registered for a given language.</summary>
    public IReadOnlyList<IProjectTemplate> ForLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        return _byLanguage.TryGetValue(language, out var group) ? group : [];
    }
}
