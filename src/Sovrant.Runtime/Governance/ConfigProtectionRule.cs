using Microsoft.Extensions.FileSystemGlobbing;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Checks whether a file path matches any protected file pattern.
/// Protects critical config files from unintended modification.
/// </summary>
internal sealed class ConfigProtectionRule
{
    private static readonly string[] BuiltInProtectedPatterns =
    [
        ".editorconfig",
        ".eslintrc",
        ".eslintrc.*",
        ".prettierrc",
        ".prettierrc.*",
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "global.json",
        "NuGet.Config",
        ".github/workflows/*",
    ];

    private readonly Matcher _matcher;

    internal ConfigProtectionRule(IEnumerable<string>? extraPatterns = null)
    {
        _matcher = new Matcher();
        foreach (var pattern in BuiltInProtectedPatterns)
            _matcher.AddInclude(pattern);

        if (extraPatterns is not null)
        {
            foreach (var pattern in extraPatterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                    _matcher.AddInclude(pattern);
            }
        }
    }

    /// <summary>Returns true if the file path matches a protected pattern.</summary>
    internal bool IsProtected(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        // Normalize to forward slashes for matching
        var normalized = filePath.Replace('\\', '/');

        // Try matching the full path and also just the filename
        var fileName = Path.GetFileName(normalized);
        return _matcher.Match(normalized).HasMatches || _matcher.Match(fileName).HasMatches;
    }
}
