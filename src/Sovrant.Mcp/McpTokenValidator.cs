namespace Sovrant.Mcp;

/// <summary>
/// Validates the bearer token for MCP server mode.
/// </summary>
/// <remarks>
/// If <c>SOVRANT_MCP_TOKEN</c> is set in the environment, callers must supply
/// a matching token via <c>--token</c>. Errors are written to stderr so they
/// do not corrupt the stdout JSON-RPC transport.
/// </remarks>
public static class McpTokenValidator
{
    private const string EnvVar = "SOVRANT_MCP_TOKEN";

    /// <summary>
    /// Validates the provided token against <c>SOVRANT_MCP_TOKEN</c>.
    /// Returns <see langword="null"/> when access is granted (no env var set, or tokens match).
    /// Returns a human-readable error message when access is denied.
    /// </summary>
    public static string? Validate(string? providedToken)
    {
        var required = Environment.GetEnvironmentVariable(EnvVar);

        // No env var set → open mode, no auth required.
        if (string.IsNullOrWhiteSpace(required))
            return null;

        if (string.IsNullOrWhiteSpace(providedToken))
            return $"SOVRANT_MCP_TOKEN is set but --token was not provided. Access denied.";

        if (!string.Equals(providedToken, required, StringComparison.Ordinal))
            return "--token does not match SOVRANT_MCP_TOKEN. Access denied.";

        return null;
    }
}
