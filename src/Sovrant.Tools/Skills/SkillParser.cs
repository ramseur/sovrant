namespace Sovrant.Tools.Skills;

/// <summary>
/// Parses <c>.md</c> files with YAML frontmatter into <see cref="SkillDefinition"/> instances.
/// Format mirrors the agent template pattern from Phase 21.5.
/// </summary>
internal static class SkillParser
{
    /// <summary>Parses a skill definition from file content. Returns null if invalid.</summary>
    internal static SkillDefinition? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.ReplaceLineEndings("\n");

        if (!text.StartsWith("---\n", StringComparison.Ordinal))
            return null;

        var endIdx = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (endIdx < 0)
            return null;

        var frontMatter = text[4..endIdx];
        var body = text[(endIdx + 5)..].Trim();

        var meta = ParseFrontMatter(frontMatter);

        if (!meta.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            return null;

        meta.TryGetValue("description", out var description);
        description ??= string.Empty;

        meta.TryGetValue("trigger", out var trigger);
        trigger ??= string.Empty;

        IReadOnlyList<string> agents = meta.TryGetValue("agents", out var agentsStr)
            ? ParseList(agentsStr)
            : [];

        IReadOnlyList<string> tools = meta.TryGetValue("tools", out var toolsStr)
            ? ParseList(toolsStr)
            : [];

        return new SkillDefinition(
            name.Trim(),
            description.Trim(),
            trigger.Trim(),
            agents,
            tools,
            string.IsNullOrWhiteSpace(body) ? $"Execute the {name} skill." : body);
    }

    /// <summary>Parses a skill definition from a file path. Returns null if file is invalid.</summary>
    internal static SkillDefinition? ParseFile(string path)
    {
        if (!File.Exists(path))
            return null;
        var text = File.ReadAllText(path);
        return Parse(text);
    }

    private static Dictionary<string, string> ParseFrontMatter(string frontMatter)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in frontMatter.Split('\n'))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            meta[key] = value;
        }
        return meta;
    }

    /// <summary>Parses YAML inline list syntax: <c>[A, B, C]</c> or bare <c>A, B, C</c>.</summary>
    internal static IReadOnlyList<string> ParseList(string value)
    {
        var v = value.Trim();
        if (v.StartsWith('[') && v.EndsWith(']'))
            v = v[1..^1];

        return [.. v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
