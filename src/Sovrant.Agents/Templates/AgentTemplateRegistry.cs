using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, AgentTemplate> _templates;
    private readonly ILogger<AgentTemplateRegistry> _logger;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load user template from '{Path}': {Error}")]
    private static partial void LogLoadError(ILogger logger, string path, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded user template '{Name}' from '{Path}'")]
    private static partial void LogLoaded(ILogger logger, string name, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "User template '{Name}' overrides built-in")]
    private static partial void LogOverride(ILogger logger, string name);

    /// <summary>Path where user-authored agents are written. Relative to working directory.</summary>
    public static readonly string UserAgentsDir = Path.Combine(".sovrant", "agents");

    public AgentTemplateRegistry(ILogger<AgentTemplateRegistry> logger, IEnumerable<ITemplateLoader>? loaders = null)
    {
        _logger = logger;
        _templates = new ConcurrentDictionary<string, AgentTemplate>(StringComparer.OrdinalIgnoreCase);

        // Tier 1 (lowest priority): built-in templates from the install directory
        var assemblyDir = Path.GetDirectoryName(typeof(AgentTemplateRegistry).Assembly.Location) ?? ".";
        LoadFromDirectory(Path.Combine(assemblyDir, "agents"), isBuiltIn: true);

        // Tier 2: project-local .sovrant/agents/ (overrides install-dir templates)
        LoadFromDirectory(UserAgentsDir, isBuiltIn: false);

        // Tier 3: additional loaders (e.g. database, cloud) — highest priority
        if (loaders is not null)
        {
            foreach (var loader in loaders)
            {
                foreach (var (name, template) in loader.Load())
                {
                    if (_templates.ContainsKey(name))
                        LogOverride(_logger, name);
                    _templates[name] = template;
                }
            }
        }
    }

    /// <summary>All templates currently registered (built-in + user-defined).</summary>
    public IReadOnlyCollection<AgentTemplate> All => _templates.Values.ToArray();

    /// <summary>Returns the template with the given name, or <see langword="null"/> if not found.</summary>
    public AgentTemplate? TryGet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _templates.GetValueOrDefault(name);
    }

    /// <summary>
    /// Adds or replaces a template in the live registry (does not write to disk).
    /// Call after <see cref="AgentDefinitionWriter.SaveAsync"/> to refresh the in-memory state.
    /// </summary>
    public void Register(AgentTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        _templates[template.Name] = template;
    }

    /// <summary>
    /// Removes a user-defined template from the live registry.
    /// Returns <see langword="false"/> if the name was not found or the template is built-in.
    /// Does not touch disk — call <see cref="AgentDefinitionWriter.Delete"/> first.
    /// </summary>
    public bool Unregister(string name)
    {
        if (_templates.TryGetValue(name, out var existing) && existing.IsBuiltIn)
            return false;
        return _templates.TryRemove(name, out _);
    }

    // ── Internal loading ──────────────────────────────────────────────────────

    /// <summary>Kept for backward compat with callers that used the old name.</summary>
    internal void LoadUserTemplates(string directory) => LoadFromDirectory(directory, isBuiltIn: false);

    internal void LoadFromDirectory(string directory, bool isBuiltIn)
    {
        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.EnumerateFiles(directory, "*.md"))
        {
            try
            {
                var template = ParseTemplateFile(file, isBuiltIn);
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

    internal static AgentTemplate? ParseTemplateFile(string path, bool isBuiltIn = false)
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

        meta.TryGetValue("description", out var description);

        var systemPrompt = string.IsNullOrWhiteSpace(body) ? $"You are a {name} agent." : body;

        return new AgentTemplate(name.Trim(), role, level, allowedTools, systemPrompt, isBuiltIn, description?.Trim());
    }

    /// <summary>Serializes a template back to the markdown-with-frontmatter file format.</summary>
    public static string Serialize(AgentTemplate t)
    {
        ArgumentNullException.ThrowIfNull(t);
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine(string.Create(ic, $"name: {t.Name}"));
        if (!string.IsNullOrWhiteSpace(t.Description))
            sb.AppendLine(string.Create(ic, $"description: {t.Description}"));
        sb.AppendLine(string.Create(ic, $"role: {t.Role}"));
        sb.AppendLine(string.Create(ic, $"recommended_level: {t.RecommendedLevel}"));
        if (t.AllowedTools.Count > 0)
            sb.AppendLine(string.Create(ic, $"allowed_tools: [{string.Join(", ", t.AllowedTools)}]"));
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(t.SystemPrompt);
        return sb.ToString();
    }

    /// <summary>Parses YAML inline list syntax: <c>[A, B, C]</c> or bare <c>A, B, C</c>.</summary>
    private static IReadOnlyList<string> ParseList(string value)
    {
        var v = value.Trim();
        if (v.StartsWith('[') && v.EndsWith(']'))
            v = v[1..^1];

        return [.. v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
