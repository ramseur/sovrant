namespace Sovrant.Server.Tests;

/// <summary>Tests for <see cref="InputValidation"/> regex validators.</summary>
public sealed class InputValidationTests
{
    // ── Session ID ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("my-session")]
    [InlineData("session_1")]
    [InlineData("user.project:session")]
    [InlineData("abc@def")]
    [InlineData("a")]
    public void IsValidSessionId_ValidIds_ReturnsTrue(string id) =>
        Assert.True(InputValidation.IsValidSessionId(id));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("path/../traversal")]
    [InlineData("has;semicolon")]
    [InlineData("has\nnewline")]
    [InlineData("has<angle>brackets")]
    public void IsValidSessionId_InvalidIds_ReturnsFalse(string? id) =>
        Assert.False(InputValidation.IsValidSessionId(id));

    [Fact]
    public void IsValidSessionId_TooLong_ReturnsFalse() =>
        Assert.False(InputValidation.IsValidSessionId(new string('a', 129)));

    [Fact]
    public void IsValidSessionId_MaxLength_ReturnsTrue() =>
        Assert.True(InputValidation.IsValidSessionId(new string('a', 128)));

    // ── Model Name ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("claude-3.5-sonnet")]
    [InlineData("anthropic/claude-opus-4-6")]
    [InlineData("ollama:llama3")]
    public void IsValidModelName_ValidNames_ReturnsTrue(string model) =>
        Assert.True(InputValidation.IsValidModelName(model));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("model with spaces")]
    [InlineData("model;injection")]
    [InlineData("model\nbreak")]
    public void IsValidModelName_InvalidNames_ReturnsFalse(string? model) =>
        Assert.False(InputValidation.IsValidModelName(model));

    [Fact]
    public void IsValidModelName_TooLong_ReturnsFalse() =>
        Assert.False(InputValidation.IsValidModelName(new string('m', 129)));
}
