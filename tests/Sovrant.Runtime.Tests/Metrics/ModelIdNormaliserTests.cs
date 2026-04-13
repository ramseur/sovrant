using Sovrant.Runtime.Metrics;

namespace Sovrant.Runtime.Tests.Metrics;

public sealed class ModelIdNormaliserTests
{
    private readonly ModelIdNormaliser _normaliser = new();

    [Theory]
    [InlineData("claude-sonnet-4-6", "anthropic/claude-sonnet-4.5")]
    [InlineData("claude-opus-4-6", "anthropic/claude-opus-4.6")]
    [InlineData("claude-haiku-4-5-20251001", "anthropic/claude-haiku-4.5")]
    [InlineData("gpt-4o", "openai/gpt-4o")]
    [InlineData("gpt-4o-mini", "openai/gpt-4o-mini")]
    [InlineData("gpt-4.1", "openai/gpt-4.1")]
    [InlineData("gemini-2.0-flash", "google/gemini-2.0-flash")]
    [InlineData("deepseek-chat", "deepseek/deepseek-chat-v3")]
    [InlineData("deepseek-r1", "deepseek/deepseek-r1")]
    [InlineData("mistral-large", "mistralai/mistral-large")]
    public void Normalise_KnownModels_ReturnsOpenRouterId(string input, string expected)
    {
        var result = _normaliser.Normalise(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalise_UnknownModel_ReturnsNull()
    {
        var result = _normaliser.Normalise("some-random-model-xyz");
        Assert.Null(result);
    }

    [Fact]
    public void Normalise_AlreadyOpenRouterFormat_ReturnsSame()
    {
        var result = _normaliser.Normalise("anthropic/claude-sonnet-4.5");
        Assert.Equal("anthropic/claude-sonnet-4.5", result);
    }

    [Fact]
    public void Normalise_EmptyOrNull_ReturnsNull()
    {
        Assert.Null(_normaliser.Normalise(""));
        Assert.Null(_normaliser.Normalise("   "));
        Assert.Null(_normaliser.Normalise(null!));
    }

    [Fact]
    public void Normalise_CaseInsensitive()
    {
        var result = _normaliser.Normalise("GPT-4O");
        Assert.Equal("openai/gpt-4o", result);
    }

    [Theory]
    [InlineData("gpt-4o-2024-11-20", "openai/gpt-4o")]
    [InlineData("claude-sonnet-4-6-20250514", "anthropic/claude-sonnet-4.5")]
    public void Normalise_DateSuffix_MatchesBaseModel(string input, string expected)
    {
        var result = _normaliser.Normalise(input);
        Assert.Equal(expected, result);
    }
}
