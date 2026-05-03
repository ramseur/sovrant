namespace Sovrant.Runtime.Documents.Templates;

/// <summary>Parses <c>.md</c> files with YAML frontmatter into <see cref="UserDocumentTemplate"/> instances.</summary>
internal static class UserDocumentTemplateParser
{
    internal static UserDocumentTemplate? Parse(string text, string slug, UserTemplateTier tier, string sourcePath)
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
        meta.TryGetValue("industry", out var industry);
        meta.TryGetValue("default_format", out var defaultFormat);

        return new UserDocumentTemplate(
            slug,
            name.Trim(),
            (description ?? string.Empty).Trim(),
            (industry ?? "general").Trim(),
            (defaultFormat ?? "markdown").Trim(),
            body,
            tier,
            sourcePath);
    }

    internal static UserDocumentTemplate? ParseFile(string path, UserTemplateTier tier)
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
