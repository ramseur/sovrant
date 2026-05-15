using System.Text.RegularExpressions;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Scans text for common secret patterns: AWS keys, API keys, JWTs, private keys, passwords.
/// Supports additional user-configured patterns from <see cref="GovernanceConfig.SecretPatterns"/>.
/// </summary>
internal sealed partial class SecretDetector
{
    private readonly List<Regex> _extraPatterns = [];

    internal SecretDetector(IEnumerable<string>? extraPatterns = null)
    {
        if (extraPatterns is null)
            return;
        foreach (var pattern in extraPatterns)
        {
            try
            {
                _extraPatterns.Add(new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1)));
            }
            catch (ArgumentException)
            {
                // Skip invalid user-provided patterns
            }
        }
    }

    /// <summary>Returns the first finding description, or null if clean.</summary>
    internal string? Scan(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        if (AwsKeyPattern().IsMatch(text))
            return "AWS access key detected";

        if (GenericApiKeyPattern().IsMatch(text))
            return "API key/secret key pattern detected";

        if (PrivateKeyPattern().IsMatch(text))
            return "Private key detected";

        if (JwtPattern().IsMatch(text))
            return "JWT token detected";

        if (GenericPasswordPattern().IsMatch(text))
            return "Hardcoded password detected";

        if (GenericTokenPattern().IsMatch(text))
            return "Token/bearer credential detected";

        foreach (var extra in _extraPatterns)
        {
            if (extra.IsMatch(text))
                return $"Custom secret pattern matched: {extra}";
        }

        return null;
    }

    // AWS access key ID
    [GeneratedRegex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled)]
    private static partial Regex AwsKeyPattern();

    // Generic API key patterns (sk-..., api_key=..., apikey=...)
    [GeneratedRegex(@"(?:sk-[a-zA-Z0-9]{20,}|api[_-]?key\s*[:=]\s*[""']?[a-zA-Z0-9]{20,})", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GenericApiKeyPattern();

    // PEM private keys
    [GeneratedRegex(@"-----BEGIN\s+(?:RSA\s+)?PRIVATE\s+KEY", RegexOptions.Compiled)]
    private static partial Regex PrivateKeyPattern();

    // JWT tokens (three base64url segments separated by dots)
    [GeneratedRegex(@"eyJ[a-zA-Z0-9_-]{10,}\.eyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}", RegexOptions.Compiled)]
    private static partial Regex JwtPattern();

    // Hardcoded passwords in assignments
    [GeneratedRegex(@"(?:password|passwd|pwd)\s*[:=]\s*[""'][^""']{8,}[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GenericPasswordPattern();

    // Bearer tokens and generic token assignments
    [GeneratedRegex(@"(?:bearer\s+[a-zA-Z0-9_\-.]{20,}|token\s*[:=]\s*[""'][a-zA-Z0-9_\-.]{20,}[""'])", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GenericTokenPattern();
}
