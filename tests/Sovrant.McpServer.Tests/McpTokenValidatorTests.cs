namespace Sovrant.McpServer.Tests;

public sealed class McpTokenValidatorTests
{
    // Helper: run a test with SOVRANT_MCP_TOKEN set to a specific value and restore it after.
    private static T WithEnv<T>(string? value, Func<T> action)
    {
        var prev = Environment.GetEnvironmentVariable("SOVRANT_MCP_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOKEN", value);
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOKEN", prev);
        }
    }

    [Fact]
    public void Validate_NoEnvVar_ReturnsNull()
    {
        // No token required — open mode.
        var result = WithEnv(null, () => McpTokenValidator.Validate(null));
        Assert.Null(result);
    }

    [Fact]
    public void Validate_NoEnvVar_AnyProvidedTokenGrantsAccess()
    {
        var result = WithEnv(null, () => McpTokenValidator.Validate("anything"));
        Assert.Null(result);
    }

    [Fact]
    public void Validate_EmptyEnvVar_ReturnsNull()
    {
        var result = WithEnv("", () => McpTokenValidator.Validate(null));
        Assert.Null(result);
    }

    [Fact]
    public void Validate_WhitespaceEnvVar_ReturnsNull()
    {
        var result = WithEnv("   ", () => McpTokenValidator.Validate(null));
        Assert.Null(result);
    }

    [Fact]
    public void Validate_EnvVarSet_NoTokenProvided_ReturnsError()
    {
        var result = WithEnv("secret123", () => McpTokenValidator.Validate(null));
        Assert.NotNull(result);
        Assert.Contains("SOVRANT_MCP_TOKEN", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EnvVarSet_EmptyTokenProvided_ReturnsError()
    {
        var result = WithEnv("secret123", () => McpTokenValidator.Validate(""));
        Assert.NotNull(result);
    }

    [Fact]
    public void Validate_EnvVarSet_WrongToken_ReturnsError()
    {
        var result = WithEnv("secret123", () => McpTokenValidator.Validate("wrong"));
        Assert.NotNull(result);
        Assert.Contains("Access denied", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EnvVarSet_CorrectToken_ReturnsNull()
    {
        var result = WithEnv("secret123", () => McpTokenValidator.Validate("secret123"));
        Assert.Null(result);
    }

    [Fact]
    public void Validate_TokenComparison_IsCaseSensitive()
    {
        var result = WithEnv("Secret123", () => McpTokenValidator.Validate("secret123"));
        Assert.NotNull(result); // Wrong case → denied.
    }

    [Fact]
    public void Validate_TokenComparison_ExactMatch_ReturnsNull()
    {
        var result = WithEnv("MyToken-xyz_99", () => McpTokenValidator.Validate("MyToken-xyz_99"));
        Assert.Null(result);
    }
}
