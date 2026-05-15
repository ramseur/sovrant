using System.Text.RegularExpressions;

namespace Sovrant.Runtime.Preferences;

/// <summary>
/// Validation helpers for user-preference values read from
/// <see cref="IUserPreferenceStore"/> before they are applied to
/// <see cref="Sovrant.Runtime.Config.SovrantConfig"/>. The preference store is
/// trust-but-verify: a corrupted or tampered row must not be able to inject
/// arbitrary URLs or extreme numeric values into the runtime singleton.
/// </summary>
internal static partial class PreferenceValidation
{
    // Mirrors Sovrant.Server.InputValidation.ModelNamePattern — kept private to
    // Runtime because the Server type is internal to its assembly.
    [GeneratedRegex(@"^[a-zA-Z0-9._/:\-]{1,128}$")]
    private static partial Regex ModelNamePattern();

    public const int MinMaxTokens = 1;
    public const int MaxMaxTokens = 2_000_000;

    public static bool IsValidModelName(string? model) =>
        model is not null && ModelNamePattern().IsMatch(model);

    public static int ClampMaxTokens(int requested) =>
        Math.Clamp(requested, MinMaxTokens, MaxMaxTokens);

    public static bool IsAllowedBaseUrlScheme(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}
