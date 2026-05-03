using Microsoft.Extensions.Logging;

namespace Sovrant.Tools.Templates;

/// <summary>
/// Discovers and indexes user-authored markdown tool templates from three tiers:
/// built-in (assembly <c>tools/</c>) → global (<c>~/.sovrant/tools/</c>) →
/// project-local (<c>.sovrant/tools/</c>). Mirrors <c>SkillRegistry</c>.
/// </summary>
public sealed partial class UserToolTemplateRegistry
{
    private readonly Dictionary<string, UserToolTemplate> _bySlug;
    private readonly ILogger<UserToolTemplateRegistry> _logger;

    private static readonly string GlobalDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "tools");

    private static string ProjectDir =>
        Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "tools");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load tool template from '{Path}': {Error}")]
    private static partial void LogLoadError(ILogger logger, string path, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded tool template '{Slug}' from '{Path}'")]
    private static partial void LogLoaded(ILogger logger, string slug, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tool template '{Slug}' overridden by higher-priority source")]
    private static partial void LogOverride(ILogger logger, string slug);

    public UserToolTemplateRegistry(ILogger<UserToolTemplateRegistry> logger)
    {
        _logger = logger;
        _bySlug = new Dictionary<string, UserToolTemplate>(StringComparer.OrdinalIgnoreCase);

        var assemblyDir = Path.GetDirectoryName(typeof(UserToolTemplateRegistry).Assembly.Location) ?? ".";
        LoadFrom(Path.Combine(assemblyDir, "tools"), UserToolTier.BuiltIn);
        LoadFrom(GlobalDir, UserToolTier.Global);
        LoadFrom(ProjectDir, UserToolTier.Project);
    }

    public IReadOnlyCollection<UserToolTemplate> All => _bySlug.Values;

    public UserToolTemplate? TryGet(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return _bySlug.GetValueOrDefault(slug);
    }

    public static string GlobalPathFor(string slug) =>
        Path.Combine(GlobalDir, $"{slug}.md");

    public void SaveGlobal(string slug, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(markdown);
        Directory.CreateDirectory(GlobalDir);
        var path = GlobalPathFor(slug);
        File.WriteAllText(path, markdown);

        var parsed = UserToolTemplateParser.ParseFile(path, UserToolTier.Global);
        if (parsed is not null)
            _bySlug[slug] = parsed;
    }

    public bool DeleteGlobal(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var path = GlobalPathFor(slug);
        if (!File.Exists(path)) return false;
        File.Delete(path);

        _bySlug.Remove(slug);
        var assemblyDir = Path.GetDirectoryName(typeof(UserToolTemplateRegistry).Assembly.Location) ?? ".";
        var builtInPath = Path.Combine(assemblyDir, "tools", $"{slug}.md");
        if (File.Exists(builtInPath))
        {
            var parsed = UserToolTemplateParser.ParseFile(builtInPath, UserToolTier.BuiltIn);
            if (parsed is not null) _bySlug[slug] = parsed;
        }
        return true;
    }

    internal void LoadFrom(string directory, UserToolTier tier)
    {
        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.EnumerateFiles(directory, "*.md"))
        {
            try
            {
                var template = UserToolTemplateParser.ParseFile(file, tier);
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
