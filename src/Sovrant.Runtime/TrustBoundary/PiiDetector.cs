using System.Text.RegularExpressions;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>Detects common PII patterns: emails, phone numbers, SSNs, credit cards, internal IPs.</summary>
public sealed partial class PiiDetector : IPatternDetector
{
    // ── Compiled regex patterns ────────────────────────────────────────

    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<!\d)(\+?1[\s.\-]?)?\(?\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{4}(?!\d)", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"(?<!\d)\d{3}[\-\s]\d{2}[\-\s]\d{4}(?!\d)", RegexOptions.Compiled)]
    private static partial Regex SsnRegex();

    [GeneratedRegex(@"(?<!\d)[3-6]\d{3}[\-\s]?\d{4}[\-\s]?\d{4}[\-\s]?\d{4}(?!\d)", RegexOptions.Compiled)]
    private static partial Regex CreditCardRegex();

    // Internal/private IP ranges: 10.x.x.x, 172.16-31.x.x, 192.168.x.x
    [GeneratedRegex(@"(?<!\d)(?:10\.\d{1,3}\.\d{1,3}\.\d{1,3}|172\.(?:1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3})(?!\d)", RegexOptions.Compiled)]
    private static partial Regex InternalIpRegex();

    private static readonly (Func<Regex> Pattern, string Category)[] s_patterns =
    [
        (EmailRegex, "EMAIL"),
        (PhoneRegex, "PHONE"),
        (SsnRegex, "SSN"),
        (CreditCardRegex, "CARD"),
        (InternalIpRegex, "IP"),
    ];

    public IReadOnlyList<Detection> Detect(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var results = new List<Detection>();
        foreach (var (patternFactory, category) in s_patterns)
        {
            foreach (Match match in patternFactory().Matches(text))
            {
                results.Add(new Detection(match.Index, match.Length, category, match.Value));
            }
        }

        // Sort by position so callers can process left-to-right.
        results.Sort((a, b) => a.Start.CompareTo(b.Start));
        return results;
    }
}
