using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Templates;

/// <summary>
/// Provides lookup and enumeration of agent templates.
/// Built-in templates are always available; user-defined templates in
/// <c>.sovrant/agents/*.md</c> are merged on top and can override built-ins.
/// </summary>
public sealed partial class AgentTemplateRegistry
{
    private readonly Dictionary<string, AgentTemplate> _templates;
    private readonly ILogger<AgentTemplateRegistry> _logger;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load user template from '{Path}': {Error}")]
    private static partial void LogLoadError(ILogger logger, string path, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded user template '{Name}' from '{Path}'")]
    private static partial void LogLoaded(ILogger logger, string name, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "User template '{Name}' overrides built-in")]
    private static partial void LogOverride(ILogger logger, string name);

    public AgentTemplateRegistry(ILogger<AgentTemplateRegistry> logger)
    {
        _logger = logger;

        _templates = new Dictionary<string, AgentTemplate>(StringComparer.OrdinalIgnoreCase);

        // Tier 1 (lowest priority): built-in templates from the install directory
        var assemblyDir = Path.GetDirectoryName(typeof(AgentTemplateRegistry).Assembly.Location) ?? ".";
        LoadUserTemplates(Path.Combine(assemblyDir, "agents"));

        // Tier 2: project-local .sovrant/agents/ (overrides install-dir templates)
        LoadUserTemplates(Path.Combine(".sovrant", "agents"));
    }

    /// <summary>All templates currently registered (built-in + user-defined).</summary>
    public IReadOnlyCollection<AgentTemplate> All => _templates.Values;

    /// <summary>Returns the template with the given name, or <see langword="null"/> if not found.</summary>
    public AgentTemplate? TryGet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _templates.GetValueOrDefault(name);
    }

    /// <summary>
    /// Loads <c>*.md</c> files from <paramref name="directory"/> as user-defined templates.
    /// Each file must have YAML front matter with at least <c>name</c> and optional
    /// <c>role</c>, <c>recommended_level</c>, <c>allowed_tools</c>.
    /// The file body (after the closing <c>---</c>) is the system prompt.
    /// </summary>
    internal void LoadUserTemplates(string directory)
    {
        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.EnumerateFiles(directory, "*.md"))
        {
            try
            {
                var template = ParseTemplateFile(file);
                if (template is null) continue;

                if (_templates.ContainsKey(template.Name))
                    LogOverride(_logger, template.Name);

                _templates[template.Name] = template;
                LogLoaded(_logger, template.Name, file);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                LogLoadError(_logger, file, ex.Message);
            }
        }
    }

    private static AgentTemplate? ParseTemplateFile(string path)
    {
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");

        // Expect the file to start with ---
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
            return null;

        // Find closing ---
        var endIdx = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (endIdx < 0)
            return null;

        var frontMatter = text[4..endIdx];
        var body = text[(endIdx + 5)..].Trim();

        // Parse front matter key: value lines
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in frontMatter.Split('\n'))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            meta[key] = value;
        }

        if (!meta.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            return null;

        meta.TryGetValue("role", out var roleStr);
        if (!Enum.TryParse<AgentRole>(roleStr, ignoreCase: true, out var role))
            role = AgentRole.General;

        meta.TryGetValue("recommended_level", out var levelStr);
        if (!Enum.TryParse<RecommendedLevel>(levelStr, ignoreCase: true, out var level))
            level = RecommendedLevel.Standard;

        IReadOnlyList<string> allowedTools = [];
        if (meta.TryGetValue("allowed_tools", out var toolsStr))
            allowedTools = ParseList(toolsStr);

        var systemPrompt = string.IsNullOrWhiteSpace(body) ? $"You are a {name} agent." : body;

        return new AgentTemplate(name.Trim(), role, level, allowedTools, systemPrompt);
    }

    /// <summary>Parses YAML inline list syntax: <c>[A, B, C]</c> or bare <c>A, B, C</c>.</summary>
    private static IReadOnlyList<string> ParseList(string value)
    {
        // Strip optional surrounding brackets
        var v = value.Trim();
        if (v.StartsWith('[') && v.EndsWith(']'))
            v = v[1..^1];

        return [.. v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
