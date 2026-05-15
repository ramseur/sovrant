using System.Text.RegularExpressions;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>A user-defined sensitive pattern.</summary>
public sealed record CustomPattern(string Name, string RegexPattern, string Action = "redact");

/// <summary>Detects user-defined sensitive patterns loaded from configuration.</summary>
public sealed class CustomPatternRegistry : IPatternDetector
{
    private readonly List<(Regex Compiled, string Category)> _patterns;

    public CustomPatternRegistry(IEnumerable<CustomPattern>? patterns = null)
    {
        var list = new List<(Regex, string)>();
        foreach (var p in patterns ?? [])
        {
            if (string.IsNullOrWhiteSpace(p.RegexPattern)) continue;
            var compiled = new Regex(p.RegexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            // Category is the uppercase pattern name (e.g. "PROJECT_CODENAMES")
            var category = p.Name.ToUpperInvariant().Replace(' ', '_');
            list.Add((compiled, category));
        }
        _patterns = list;
    }

    public IReadOnlyList<Detection> Detect(string text)
    {
        if (string.IsNullOrEmpty(text) || _patterns.Count == 0) return [];

        var results = new List<Detection>();
        foreach (var (regex, category) in _patterns)
        {
            foreach (Match match in regex.Matches(text))
            {
                results.Add(new Detection(match.Index, match.Length, category, match.Value));
            }
        }

        results.Sort((a, b) => a.Start.CompareTo(b.Start));
        return results;
    }
}
