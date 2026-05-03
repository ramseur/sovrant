using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// Discovers and indexes user-authored markdown document templates from three tiers
/// (lowest → highest priority): built-in (assembly <c>documents/</c>) → global
/// (<c>~/.sovrant/documents/</c>) → project-local (<c>.sovrant/documents/</c>).
/// Higher-priority entries override lower by slug. Mirrors <c>SkillRegistry</c>
/// — any deviation is unintentional.
/// </summary>
public sealed partial class UserDocumentTemplateRegistry
{
    private readonly Dictionary<string, UserDocumentTemplate> _bySlug;
    private readonly ILogger<UserDocumentTemplateRegistry> _logger;

    private static readonly string GlobalDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "documents");

    private static string ProjectDir =>
        Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "documents");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load document template from '{Path}': {Error}")]
    private static partial void LogLoadError(ILogger logger, string path, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded document template '{Slug}' from '{Path}'")]
    private static partial void LogLoaded(ILogger logger, string slug, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Document template '{Slug}' overridden by higher-priority source")]
    private static partial void LogOverride(ILogger logger, string slug);

    public UserDocumentTemplateRegistry(ILogger<UserDocumentTemplateRegistry> logger)
    {
        _logger = logger;
        _bySlug = new Dictionary<string, UserDocumentTemplate>(StringComparer.OrdinalIgnoreCase);

        var assemblyDir = Path.GetDirectoryName(typeof(UserDocumentTemplateRegistry).Assembly.Location) ?? ".";
        LoadFrom(Path.Combine(assemblyDir, "documents"), UserTemplateTier.BuiltIn);
        LoadFrom(GlobalDir, UserTemplateTier.Global);
        LoadFrom(ProjectDir, UserTemplateTier.Project);
    }

    /// <summary>All templates currently registered.</summary>
    public IReadOnlyCollection<UserDocumentTemplate> All => _bySlug.Values;

    /// <summary>Returns the template with the given slug (case-insensitive), or null.</summary>
    public UserDocumentTemplate? TryGet(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return _bySlug.GetValueOrDefault(slug);
    }

    /// <summary>Path to the editable (global-tier) file for a slug — where saves land.</summary>
    public static string GlobalPathFor(string slug) =>
        Path.Combine(GlobalDir, $"{slug}.md");

    /// <summary>Saves a markdown body to the global tier and refreshes the registry. Creates the directory if needed.</summary>
    public void SaveGlobal(string slug, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(markdown);
        Directory.CreateDirectory(GlobalDir);
        var path = GlobalPathFor(slug);
        File.WriteAllText(path, markdown);

        var parsed = UserDocumentTemplateParser.ParseFile(path, UserTemplateTier.Global);
        if (parsed is not null)
            _bySlug[slug] = parsed;
    }

    /// <summary>Deletes a global-tier file and removes the entry. Built-in tier files are never touched.</summary>
    public bool DeleteGlobal(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var path = GlobalPathFor(slug);
        if (!File.Exists(path)) return false;
        File.Delete(path);

        // After deleting the global tier, re-resolve to whatever the lower tier (built-in) holds, if any.
        _bySlug.Remove(slug);
        var assemblyDir = Path.GetDirectoryName(typeof(UserDocumentTemplateRegistry).Assembly.Location) ?? ".";
        var builtInPath = Path.Combine(assemblyDir, "documents", $"{slug}.md");
        if (File.Exists(builtInPath))
        {
            var parsed = UserDocumentTemplateParser.ParseFile(builtInPath, UserTemplateTier.BuiltIn);
            if (parsed is not null) _bySlug[slug] = parsed;
        }
        return true;
    }

    internal void LoadFrom(string directory, UserTemplateTier tier)
    {
        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.EnumerateFiles(directory, "*.md"))
        {
            try
            {
                var template = UserDocumentTemplateParser.ParseFile(file, tier);
                if (template is null) continue;

                if (_bySlug.ContainsKey(template.Slug))
                    LogOverride(_logger, template.Slug);

                _bySlug[template.Slug] = template;

                LogLoaded(_logger, template.Slug, file);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                LogLoadError(_logger, file, ex.Message);
            }
        }
    }
}
