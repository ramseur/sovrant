using System.Text.RegularExpressions;

namespace Sovrant.Runtime.Documents.Templates;

/// <summary>
/// Phase 66.4 — natural-language template router. Scores registered
/// templates against a free-text prompt so agents can pick one without
/// enumerating the full list. Pure keyword matching — no LLM calls,
/// so it's cheap, deterministic, and safe to invoke on every turn.
/// </summary>
public static class TemplateMatcher
{
    private static readonly Regex s_tokenizer = new(@"[A-Za-z0-9]+", RegexOptions.Compiled);

    private static readonly HashSet<string> s_stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "draft", "for", "from",
        "generate", "give", "go", "got", "have", "i", "if", "in", "into", "is", "it",
        "let", "make", "me", "my", "need", "new", "of", "on", "or", "our", "please",
        "produce", "render", "send", "so", "that", "the", "their", "them", "then",
        "there", "they", "this", "to", "up", "us", "use", "want", "we", "what", "when",
        "which", "who", "with", "would", "write", "you", "your",
    };

    // Shorthands commonly typed in prompts that should match canonical terms.
    private static readonly Dictionary<string, string[]> s_synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nda"] = ["nondisclosure", "confidentiality"],
        ["sow"] = ["statement", "work"],
        ["bill"] = ["invoice"],
        ["receipt"] = ["invoice"],
        ["contract"] = ["agreement"],
        ["rfp"] = ["proposal"],
        ["p&l"] = ["profit", "loss", "financial"],
        ["docx"] = ["word"],
        ["pptx"] = ["powerpoint"],
        ["xlsx"] = ["excel"],
        ["cma"] = ["comparative", "market"],
        ["iep"] = ["individualized", "education"],
    };

    public sealed record TemplateMatch(IDocumentTemplate Template, int Score, IReadOnlyList<string> MatchedTerms);

    /// <summary>Returns the top-N scored matches for a natural-language prompt.</summary>
    public static IReadOnlyList<TemplateMatch> Rank(
        IEnumerable<IDocumentTemplate> templates,
        string prompt,
        int limit = 5)
    {
        ArgumentNullException.ThrowIfNull(templates);
        if (limit <= 0) limit = 5;

        var tokens = Tokenize(prompt);
        if (tokens.Count == 0)
            return [];

        var results = new List<TemplateMatch>();
        foreach (var t in templates)
        {
            var score = Score(t, tokens, out var matched);
            if (score > 0)
                results.Add(new TemplateMatch(t, score, matched));
        }

        return [.. results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Template.Id, StringComparer.OrdinalIgnoreCase)
            .Take(limit)];
    }

    private static int Score(IDocumentTemplate t, IReadOnlyList<string> tokens, out IReadOnlyList<string> matched)
    {
        var nameTokens = Tokenize(t.Name);
        var idTokens = Tokenize(t.Id);
        var industryTokens = Tokenize(t.Industry);
        var descTokens = Tokenize(t.Description ?? string.Empty);

        var hits = new List<string>();
        var score = 0;

        foreach (var token in tokens)
        {
            var matchedThisToken = false;
            foreach (var variant in Expand(token))
            {
                if (nameTokens.Contains(variant)) { score += 3; matchedThisToken = true; }
                if (idTokens.Contains(variant)) { score += 2; matchedThisToken = true; }
                if (industryTokens.Contains(variant)) { score += 2; matchedThisToken = true; }
                if (descTokens.Contains(variant)) { score += 1; matchedThisToken = true; }
            }
            if (matchedThisToken) hits.Add(token);
        }

        matched = hits;
        return score;
    }

    private static IEnumerable<string> Expand(string token)
    {
        yield return token;
        if (s_synonyms.TryGetValue(token, out var extras))
            foreach (var extra in extras) yield return extra;
    }

    private static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in s_tokenizer.Matches(text))
        {
            var tok = m.Value;
            if (tok.Length < 2) continue;
            if (s_stopwords.Contains(tok)) continue;
            set.Add(tok);
        }
        return [.. set];
    }
}
