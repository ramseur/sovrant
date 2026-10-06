namespace Sovrant.Api.Ui;

/// <summary>
/// Phase 137 — the initials shown in a user's chat avatar, shared by Web and Desktop so
/// both surfaces draw the same letters for the same person.
/// </summary>
public static class AvatarText
{
    private static readonly char[] Separators = [' ', '.', '_', '-'];

    /// <summary>
    /// Up to two uppercase initials from a display name or email: "Eric Ramseur" → "ER",
    /// "eric.ramseur@x.com" → "ER", "ramseur" → "R". Empty input → "?".
    /// </summary>
    public static string Initials(string? displayName)
    {
        var name = (displayName ?? string.Empty).Trim();
        var at = name.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0)
            name = name[..at];
        var parts = name.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.FirstOrDefault(char.IsLetterOrDigit))
            .Where(c => c != default)
            .Take(2)
            .ToArray();
        return parts.Length == 0 ? "?" : new string(parts).ToUpperInvariant();
    }
}
