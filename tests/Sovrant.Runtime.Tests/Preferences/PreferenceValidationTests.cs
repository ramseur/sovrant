using Sovrant.Runtime.Preferences;

namespace Sovrant.Runtime.Tests.Preferences;

public sealed class PreferenceValidationTests
{
    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("meta-llama/Llama-3.3-70B-Instruct-Turbo")]
    [InlineData("openrouter/anthropic/claude-3.5-sonnet:free")]
    [InlineData("claude-opus-4-7")]
    public void IsValidModelName_AcceptsRealModels(string model) =>
        Assert.True(PreferenceValidation.IsValidModelName(model));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(" leading-space")]
    [InlineData("name with space")]
    [InlineData("name\nwith\nnewline")]
    [InlineData("name;rm -rf")]
    [InlineData("name'or'1=1")]
    public void IsValidModelName_RejectsUnsafe(string? model) =>
        Assert.False(PreferenceValidation.IsValidModelName(model));

    [Fact]
    public void IsValidModelName_RejectsOver128Chars() =>
        Assert.False(PreferenceValidation.IsValidModelName(new string('a', 129)));

    [Theory]
    [InlineData(-5, PreferenceValidation.MinMaxTokens)]
    [InlineData(0, PreferenceValidation.MinMaxTokens)]
    [InlineData(1, 1)]
    [InlineData(32_000, 32_000)]
    [InlineData(int.MaxValue, PreferenceValidation.MaxMaxTokens)]
    public void ClampMaxTokens_ClampsToRange(int input, int expected) =>
        Assert.Equal(expected, PreferenceValidation.ClampMaxTokens(input));

    [Theory]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("http://localhost:11434/v1")]
    public void IsAllowedBaseUrlScheme_AcceptsHttp(string url) =>
        Assert.True(PreferenceValidation.IsAllowedBaseUrlScheme(new Uri(url)));

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/api")]
    [InlineData("javascript:alert(1)")]
    public void IsAllowedBaseUrlScheme_RejectsOther(string url) =>
        Assert.False(PreferenceValidation.IsAllowedBaseUrlScheme(new Uri(url)));
}
