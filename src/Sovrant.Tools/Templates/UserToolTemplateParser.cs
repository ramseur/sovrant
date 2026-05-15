namespace Sovrant.Tools.Templates;

/// <summary>Parses <c>.md</c> files with YAML frontmatter into <see cref="UserToolTemplate"/> instances.</summary>
internal static class UserToolTemplateParser
{
    internal static UserToolTemplate? Parse(string text, string slug, UserToolTier tier, string sourcePath)
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
        meta.TryGetValue("category", out var category);

        return new UserToolTemplate(
            slug,
            name.Trim(),
            (description ?? string.Empty).Trim(),
            (category ?? "general").Trim(),
            body,
            tier,
            sourcePath);
    }

    internal static UserToolTemplate? ParseFile(string path, UserToolTier tier)
    {
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path);
        var slug = Path.GetFileNameWithoutExtension(path);
        return Parse(text, slug, tier, path);
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
}
