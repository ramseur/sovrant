using Sovrant.Api.Ui;
using Sovrant.Desktop.Controls;
using Sovrant.Web.Services;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Phase 136 — Web and Desktop must resolve exactly the shared <see cref="IconNames"/>
/// vocabulary. The two Lucide packages ship different releases, so these also catch a
/// package update that renames a glyph (the map stops compiling or a name goes missing).
/// </summary>
public sealed class IconVocabularyTests
{
    [Fact]
    public void Names_Are_Unique_Kebab_Case()
    {
        Assert.Equal(IconNames.All.Count, IconNames.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(IconNames.All, n => Assert.Matches("^[a-z]+(-[a-z]+)*$", n));
    }

    [Fact]
    public void Web_Map_Covers_Exactly_The_Vocabulary()
    {
        Assert.Equal(IconNames.All.Order(StringComparer.Ordinal), SovrantIcons.Map.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Desktop_Map_Covers_Exactly_The_Vocabulary()
    {
        Assert.Equal(IconNames.All.Order(StringComparer.Ordinal), SovrantIconMap.Map.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Web_Renders_Every_Name_As_Decorative_Inline_Svg()
    {
        foreach (var name in IconNames.All)
        {
            var svg = SovrantIcons.Svg(name);
            Assert.StartsWith("<svg ", svg, StringComparison.Ordinal);
            Assert.Contains("stroke=\"currentColor\"", svg, StringComparison.Ordinal);
            Assert.Contains("aria-hidden=\"true\"", svg, StringComparison.Ordinal);
            Assert.True(svg.Length > 150, $"'{name}' rendered no glyph content");
        }
    }

    [Fact]
    public void Web_Svg_Applies_Size_Stroke_And_Encoded_Class()
    {
        var svg = SovrantIcons.Svg(IconNames.Close, strokeWidth: 2.4, size: 14, cssClass: "a\"b");
        Assert.Contains("width=\"14\" height=\"14\"", svg, StringComparison.Ordinal);
        Assert.Contains("stroke-width=\"2.4\"", svg, StringComparison.Ordinal);
        Assert.Contains("class=\"a&quot;b\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_Names_Render_A_Visible_Fallback_Not_Nothing()
    {
        Assert.True(SovrantIcons.Svg("no-such-icon").Length > 150);
        Assert.Equal(Lucide.Avalonia.LucideIconKind.CircleQuestionMark, SovrantIconMap.Resolve("no-such-icon"));
    }

    [Theory]
    [InlineData("Ollama", IconNames.ProviderLocal)]
    [InlineData("LM Studio", IconNames.ProviderLocal)]
    [InlineData("OpenAI", IconNames.ProviderCloud)]
    [InlineData("OpenRouter", IconNames.ProviderCloud)]
    public void Providers_Map_To_Their_Category_Icon(string provider, string expected)
    {
        Assert.Equal(expected, IconNames.ForProvider(provider));
    }
}
