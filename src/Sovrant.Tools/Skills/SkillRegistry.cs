using Microsoft.Extensions.Logging;

namespace Sovrant.Tools.Skills;

/// <summary>
/// Discovers and indexes skills from three tiers (lowest → highest priority):
/// <list type="number">
///   <item>Built-in skills from assembly install dir <c>skills/</c></item>
///   <item>Global user skills from <c>~/.sovrant/skills/</c></item>
///   <item>Project-local skills from <c>.sovrant/skills/</c></item>
/// </list>
/// Higher-priority skills override lower-priority ones by name.
/// </summary>
public sealed partial class SkillRegistry
{
    private readonly Dictionary<string, SkillDefinition> _byName;
    private readonly Dictionary<string, SkillDefinition> _byTrigger;
    private readonly Dictionary<string, SkillSource> _sources;
    private readonly ILogger<SkillRegistry> _logger;

    private static readonly string GlobalSkillsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "skills");

    private static string ProjectSkillsDir =>
        Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "skills");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load skill from '{Path}': {Error}")]
    private static partial void LogLoadError(ILogger logger, string path, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded skill '{Name}' from '{Path}'")]
    private static partial void LogLoaded(ILogger logger, string name, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skill '{Name}' overridden by higher-priority source")]
    private static partial void LogOverride(ILogger logger, string name);

    public SkillRegistry(ILogger<SkillRegistry> logger)
    {
        _logger = logger;
        _byName = new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);
        _byTrigger = new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);
        _sources = new Dictionary<string, SkillSource>(StringComparer.OrdinalIgnoreCase);

        // Tier 1 (lowest): built-in skills from assembly install dir
        var assemblyDir = Path.GetDirectoryName(typeof(SkillRegistry).Assembly.Location) ?? ".";
        LoadSkillsFrom(Path.Combine(assemblyDir, "skills"), SkillTier.BuiltIn);

        // Tier 2: global user skills
        LoadSkillsFrom(GlobalSkillsDir, SkillTier.Global);

        // Tier 3 (highest): project-local skills
        LoadSkillsFrom(ProjectSkillsDir, SkillTier.Project);
    }

    /// <summary>Source path + tier for a registered skill, or null if unknown / runtime-registered without a path.</summary>
    public SkillSource? TryGetSource(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _sources.GetValueOrDefault(name);
    }

    /// <summary>Path where a global-tier save lands for the given slug.</summary>
    public static string GlobalPathFor(string slug) =>
        Path.Combine(GlobalSkillsDir, $"{slug}.md");

    /// <summary>Saves markdown to <c>~/.sovrant/skills/{slug}.md</c>, reparses, and indexes the result.</summary>
    public void SaveGlobal(string slug, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(markdown);
        Directory.CreateDirectory(GlobalSkillsDir);
        var path = GlobalPathFor(slug);
        File.WriteAllText(path, markdown);

        var skill = SkillParser.ParseFile(path);
        if (skill is not null)
        {
            _byName[skill.Name] = skill;
            if (!string.IsNullOrWhiteSpace(skill.Trigger))
                _byTrigger[skill.Trigger] = skill;
            _sources[skill.Name] = new SkillSource(path, SkillTier.Global);
        }
    }

    /// <summary>Deletes the global-tier file for a slug. Falls back to the built-in tier if one exists.</summary>
    public bool DeleteGlobal(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var path = GlobalPathFor(slug);
        if (!File.Exists(path)) return false;
        File.Delete(path);

        // Removing the global tier means the built-in (if any) becomes the active definition.
        var assemblyDir = Path.GetDirectoryName(typeof(SkillRegistry).Assembly.Location) ?? ".";
        var builtInPath = Path.Combine(assemblyDir, "skills", $"{slug}.md");

        // Find the entry by slug (file basename) and clear it; built-in fallback handled below.
        var existing = _byName.Values.FirstOrDefault(s =>
            _sources.TryGetValue(s.Name, out var src) && src.Path == path);
        if (existing is not null)
        {
            _byName.Remove(existing.Name);
            if (!string.IsNullOrWhiteSpace(existing.Trigger))
                _byTrigger.Remove(existing.Trigger);
            _sources.Remove(existing.Name);
        }

        if (File.Exists(builtInPath))
        {
            var fallback = SkillParser.ParseFile(builtInPath);
            if (fallback is not null)
            {
                _byName[fallback.Name] = fallback;
                if (!string.IsNullOrWhiteSpace(fallback.Trigger))
                    _byTrigger[fallback.Trigger] = fallback;
                _sources[fallback.Name] = new SkillSource(builtInPath, SkillTier.BuiltIn);
            }
        }
        return true;
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

    /// <summary>Registers a skill definition (used by SkillCreateTool for runtime creation).</summary>
    public void Register(SkillDefinition skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _byName[skill.Name] = skill;
        if (!string.IsNullOrWhiteSpace(skill.Trigger))
            _byTrigger[skill.Trigger] = skill;
    }

    internal void LoadSkillsFrom(string directory, SkillTier tier = SkillTier.BuiltIn)
    {
        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.EnumerateFiles(directory, "*.md"))
        {
            try
            {
                var skill = SkillParser.ParseFile(file);
                if (skill is null) continue;

                if (_byName.ContainsKey(skill.Name))
                    LogOverride(_logger, skill.Name);

                _byName[skill.Name] = skill;
                if (!string.IsNullOrWhiteSpace(skill.Trigger))
                    _byTrigger[skill.Trigger] = skill;
                _sources[skill.Name] = new SkillSource(file, tier);

                LogLoaded(_logger, skill.Name, file);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                LogLoadError(_logger, file, ex.Message);
            }
        }
    }
}

/// <summary>Origin of a registered skill — drives editor edit/duplicate/delete affordances.</summary>
public enum SkillTier { BuiltIn, Global, Project }

/// <summary>Where a registered skill came from on disk.</summary>
public sealed record SkillSource(string Path, SkillTier Tier);
