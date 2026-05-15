namespace Sovrant.Runtime.Governance;

/// <summary>
/// Detects dangerous shell commands that should be blocked or warned about.
/// Includes a set of built-in patterns plus user-configured blocked commands.
/// </summary>
internal sealed class DangerousCommandDetector
{
    private static readonly string[] BuiltInPatterns =
    [
        "rm -rf /",
        "rm -rf /*",
        "rm -rf ~",
        "rm -rf .",
        "git push --force main",
        "git push --force master",
        "git push -f main",
        "git push -f master",
        "git reset --hard",
        "DROP TABLE",
        "DROP DATABASE",
        "TRUNCATE TABLE",
        "chmod 777",
        "chmod -R 777",
        "mkfs.",
        "dd if=/dev/zero",
        ":(){:|:&};:",
        "shutdown",
        "reboot",
        "halt",
        "poweroff",
    ];

    private readonly List<string> _patterns;

    internal DangerousCommandDetector(IEnumerable<string>? extraPatterns = null)
    {
        _patterns = [.. BuiltInPatterns];
        if (extraPatterns is not null)
        {
            foreach (var p in extraPatterns)
            {
                if (!string.IsNullOrWhiteSpace(p) && !_patterns.Contains(p, StringComparer.OrdinalIgnoreCase))
                    _patterns.Add(p);
            }
        }
    }

    /// <summary>Returns the matched dangerous pattern, or null if the command is safe.</summary>
    internal string? Check(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        foreach (var pattern in _patterns)
        {
            if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return pattern;
        }

        return null;
    }
}
