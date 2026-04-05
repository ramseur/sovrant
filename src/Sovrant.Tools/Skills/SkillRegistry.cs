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

        // Tier 1 (lowest): built-in skills from assembly install dir
        var assemblyDir = Path.GetDirectoryName(typeof(SkillRegistry).Assembly.Location) ?? ".";
        LoadSkillsFrom(Path.Combine(assemblyDir, "skills"));

        // Tier 2: global user skills
        LoadSkillsFrom(GlobalSkillsDir);

        // Tier 3 (highest): project-local skills
        LoadSkillsFrom(ProjectSkillsDir);
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

    internal void LoadSkillsFrom(string directory)
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

                LogLoaded(_logger, skill.Name, file);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                LogLoadError(_logger, file, ex.Message);
            }
        }
    }
}
