using Sovrant.Runtime.Config;

namespace Sovrant.Cli;

/// <summary>
/// Validates that an LLM API key is configured in the credential store.
/// Returns <c>null</c> when valid, or a friendly error message when none is found.
/// </summary>
internal static class ApiKeyValidator
{
    internal static string? Validate(SovrantConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            return null;

        return """
            No LLM API key configured.

            Sovrant stores credentials in the encrypted keystore — not in environment variables.
            Configure a provider via the setup wizard, or use:

              sovrant auth set llm

            to store the key securely.
            """;
    }
}
