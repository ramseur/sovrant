using System.Text.RegularExpressions;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>Configuration for the corporate data detector.</summary>
public sealed class CorporateDataConfig
{
    /// <summary>Internal domain suffixes (e.g. "acme.com", "acme-corp.internal").</summary>
    public IReadOnlyList<string> CorporateDomains { get; init; } = [];

    /// <summary>Domains that should never be redacted (e.g. "github.com", "stackoverflow.com").</summary>
    public IReadOnlyList<string> AllowList { get; init; } = [];
}

/// <summary>Detects corporate-sensitive patterns: connection strings, API keys, internal hostnames, cloud ARNs.</summary>
public sealed partial class CorporateDataDetector : IPatternDetector
{
    private readonly CorporateDataConfig _config;

    // ── Compiled regex patterns ────────────────────────────────────────

    // Connection strings: postgres://, mongodb://, mysql://, Server=...;
    [GeneratedRegex(@"(?:postgres(?:ql)?|mysql|mongodb(?:\+srv)?|mssql|redis|amqp)://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringUrlRegex();

    [GeneratedRegex(@"(?:Server|Data Source|Host)=[^;\s]+(?:;[^;\s]+)*", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringKvRegex();

    // API keys / tokens: common prefixes
    [GeneratedRegex(@"(?:sk|pk|ghp|gho|ghu|ghs|ghr|glpat|xox[bpras]|AKIA|ASIA|AIza|eyJ)[\w\-]{10,}", RegexOptions.Compiled)]
    private static partial Regex ApiKeyRegex();

    // Bearer tokens in text
    [GeneratedRegex(@"Bearer\s+[\w\-\.]{20,}", RegexOptions.Compiled)]
    private static partial Regex BearerTokenRegex();

    // AWS ARNs
    [GeneratedRegex(@"arn:aws[\w\-]*:[\w\-]+:[\w\-]*:\d{12}:\S+", RegexOptions.Compiled)]
    private static partial Regex AwsArnRegex();

    // Azure resource URLs
    [GeneratedRegex(@"https?://[\w\-]+\.(?:blob|table|queue|file|dfs)\.core\.windows\.net\S*", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex AzureBlobRegex();

    // GCP resource URLs
    [GeneratedRegex(@"https?://[\w\-]+\.googleapis\.com\S*", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GcpResourceRegex();

    public CorporateDataDetector(CorporateDataConfig? config = null)
    {
        _config = config ?? new CorporateDataConfig();
    }

    public IReadOnlyList<Detection> Detect(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var results = new List<Detection>();

        // Static patterns
        AddMatches(results, text, ConnectionStringUrlRegex(), "CONNECTION_STRING");
        AddMatches(results, text, ConnectionStringKvRegex(), "CONNECTION_STRING");
        AddMatches(results, text, ApiKeyRegex(), "API_KEY");
        AddMatches(results, text, BearerTokenRegex(), "API_KEY");
        AddMatches(results, text, AwsArnRegex(), "CLOUD_RESOURCE");
        AddMatches(results, text, AzureBlobRegex(), "CLOUD_RESOURCE");
        AddMatches(results, text, GcpResourceRegex(), "CLOUD_RESOURCE");

        // Dynamic: internal hostnames based on configured domain suffixes
        DetectInternalHostnames(results, text);

        results.Sort((a, b) => a.Start.CompareTo(b.Start));
        return results;
    }

    private void DetectInternalHostnames(List<Detection> results, string text)
    {
        foreach (var domain in _config.CorporateDomains)
        {
            if (string.IsNullOrWhiteSpace(domain)) continue;

            // Match hostnames ending in this domain suffix (e.g. "api.acme-corp.internal")
            var escapedDomain = Regex.Escape(domain);
            var pattern = new Regex($@"[\w\-]+(?:\.[\w\-]+)*\.{escapedDomain}", RegexOptions.IgnoreCase);

            foreach (Match match in pattern.Matches(text))
            {
                // Skip if the match is in the allow list
                if (IsAllowListed(match.Value)) continue;
                results.Add(new Detection(match.Index, match.Length, "HOSTNAME", match.Value));
            }
        }
    }

    private bool IsAllowListed(string value)
    {
        foreach (var allowed in _config.AllowList)
        {
            if (value.Contains(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void AddMatches(List<Detection> results, string text, Regex regex, string category)
    {
        foreach (Match match in regex.Matches(text))
        {
            results.Add(new Detection(match.Index, match.Length, category, match.Value));
        }
    }
}
