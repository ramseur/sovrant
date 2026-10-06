using System.Text.RegularExpressions;
using Sovrant.Api.Ui;
using Sovrant.Desktop.Controls;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Phase 136 — the design mocks (docs/design/web.html, desktop.html) document the icon
/// vocabulary in their <c>VOCAB</c> list. Keep that list, <see cref="IconNames"/>, and the
/// Desktop glyph map in step so the mock stays the source of truth for what each name draws.
/// </summary>
public sealed partial class MockVocabularyTests
{
    // A VOCAB row: ['name','WebGlyph','DesktopGlyph','Group','replaces']
    [GeneratedRegex(@"^\s*\['(?<name>[a-z-]+)','(?<web>\w+)','(?<desk>\w+)','[^']+',", RegexOptions.Multiline)]
    private static partial Regex VocabRow();

    public static TheoryData<string> Mocks() => new() { "web.html", "desktop.html" };

    [Theory]
    [MemberData(nameof(Mocks))]
    public void Mock_Vocabulary_Matches_IconNames(string mock)
    {
        var rows = Rows(mock);
        Assert.Equal(IconNames.All.Order(StringComparer.Ordinal), rows.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Mocks))]
    public void Mock_Desktop_Glyphs_Match_The_Desktop_Map(string mock)
    {
        foreach (var (name, desktopGlyph) in Rows(mock))
            Assert.Equal(desktopGlyph, SovrantIconMap.Map[name].ToString());
    }

    private static Dictionary<string, string> Rows(string mock)
    {
        var html = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "design", mock));
        return VocabRow().Matches(html).ToDictionary(m => m.Groups["name"].Value, m => m.Groups["desk"].Value, StringComparer.Ordinal);
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
