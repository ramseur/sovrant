using System.Text;
using System.Text.RegularExpressions;

namespace Sovrant.Tools.Quality;

/// <summary>
/// Analyses git diff output for common issues: debug code, secrets, unintended files.
/// </summary>
internal static partial class DiffReviewer
{
    /// <summary>Reviews a git diff string and returns findings (empty string = clean).</summary>
    internal static string Review(string diff)
    {
        if (string.IsNullOrWhiteSpace(diff))
            return string.Empty;

        var findings = new List<string>();

        // Check for debug statements
        if (DebugPattern().IsMatch(diff))
            findings.Add("Debug code detected (console.log, Console.WriteLine debug, debugger, print())");

        // Check for potential secrets
        if (SecretPattern().IsMatch(diff))
            findings.Add("Potential secret/key pattern detected in diff");

        // Check for common unintended files
        if (UnintendedFilePattern().IsMatch(diff))
            findings.Add("Potentially unintended files in diff (.env, .pem, credentials, node_modules)");

        // Check for TODO/FIXME/HACK markers in added lines
        if (TodoPattern().IsMatch(diff))
            findings.Add("TODO/FIXME/HACK comments found in added lines");

        return findings.Count == 0
            ? string.Empty
            : string.Join('\n', findings);
    }

    [GeneratedRegex(@"^\+.*(?:console\.log|Console\.WriteLine\s*\(\s*""debug|debugger;|print\s*\(\s*f?[""']debug)", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex DebugPattern();

    [GeneratedRegex(@"^\+.*(AKIA[0-9A-Z]{16}|sk-[a-zA-Z0-9]{20,}|-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY|password\s*=\s*[""'][^""']{8,})", RegexOptions.Multiline)]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"^diff --git a/.*\.(env|pem|p12|pfx)|^diff --git a/.*node_modules/|^diff --git a/.*credentials\.", RegexOptions.Multiline)]
    private static partial Regex UnintendedFilePattern();

    [GeneratedRegex(@"^\+.*(TODO|FIXME|HACK)\b", RegexOptions.Multiline)]
    private static partial Regex TodoPattern();
}
