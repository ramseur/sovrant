using System.Text.RegularExpressions;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Phase 136 — UI chrome uses <c>SovrantIcon</c>, never emoji or pictographic symbols.
/// Scans Web and Desktop source for literal emoji, HTML entities, and C# surrogate escapes.
/// Content (user text, model output) never lives in source, so any hit is chrome.
/// </summary>
public sealed partial class NoEmojiChromeTests
{
    // Pictographs (U+1F300-1FAFF), misc symbols and dingbats (U+2600-27BF: check, cross, warning, bolt, cloud),
    // geometric shapes used as icons (U+25A0-25FF: stop square, dropdown/caret triangles) and refresh arrows.
    [GeneratedRegex(@"[\u2600-\u27BF\u25A0-\u25FF\u21BA\u21BB\u27F2\u27F3]|\uD83C[\uDF00-\uDFFF]|\uD83D[\uDC00-\uDFFF]|\uD83E[\uDD00-\uDEFF]")]
    private static partial Regex LiteralEmoji();

    // HTML entities (&#x1F527; &#128279; &#10003; &#x2715; &#x25BE; &#x21BB;) and C# surrogate escapes.
    [GeneratedRegex(@"&#x(1F[0-9A-Fa-f]{3}|2[67][0-9A-Fa-f]{2}|25[A-Fa-f][0-9A-Fa-f]|21B[AB]);|&#(12[0-9]{4}|9[6-9][0-9]{2}|10[0-1][0-9]{2}|863[45]);|\\uD83[CDE]|\\u2[67][0-9A-Fa-f]{2}|\\u25[A-Fa-f][0-9A-Fa-f]")]
    private static partial Regex EscapedEmoji();

    private const string AllowMarker = "icon-scan:allow";

    public static TheoryData<string> UiProjects() => new() { "Sovrant.Web", "Sovrant.Desktop" };

    [Theory]
    [MemberData(nameof(UiProjects))]
    public void No_Emoji_In_Ui_Source(string project)
    {
        var root = Path.Combine(RepoRoot(), "src", project);
        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (!IsUiSource(file))
                continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                // A line may opt out only with an explained marker on it or just above it
                // (e.g. a browser tab title, which is plain text and cannot hold an icon).
                if (lines[i].Contains(AllowMarker, StringComparison.Ordinal) || (i > 0 && lines[i - 1].Contains(AllowMarker, StringComparison.Ordinal)))
                    continue;
                if (LiteralEmoji().IsMatch(lines[i]) || EscapedEmoji().IsMatch(lines[i]))
                    hits.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {lines[i].Trim()}");
            }
        }
        Assert.True(hits.Count == 0, $"{hits.Count} emoji used as UI chrome — use SovrantIcon instead:\n" + string.Join('\n', hits));
    }

    private static bool IsUiSource(string file)
    {
        var sep = Path.DirectorySeparatorChar;
        if (file.Contains($"{sep}bin{sep}", StringComparison.Ordinal) || file.Contains($"{sep}obj{sep}", StringComparison.Ordinal))
            return false;
        return Path.GetExtension(file) is ".razor" or ".axaml" or ".cs" or ".css" or ".js";
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Sovrant.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Sovrant.slnx not found above the test output directory");
    }
}
