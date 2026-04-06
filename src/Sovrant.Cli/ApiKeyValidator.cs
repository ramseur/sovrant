using Sovrant.Runtime.Config;

namespace Sovrant.Cli;

/// <summary>
/// Validates that at least one LLM API key is configured.
/// Returns <c>null</c> when valid, or a friendly error message when none is found.
/// </summary>
internal static class ApiKeyValidator
{
    private static readonly string[] s_envVarNames =
        ["LLM_API_KEY", "OPENAI_API_KEY", "PROVIDER_API_KEY"];

    internal static string? Validate(SovrantConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            return null;

        foreach (var name in s_envVarNames)
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                return null;
        }

        return """
            No LLM API key found.

            Sovrant needs an API key to communicate with your LLM provider.
            Set one of these environment variables:

              export LLM_API_KEY="sk-your-key-here"
              export OPENAI_API_KEY="sk-your-key-here"
              export PROVIDER_API_KEY="sk-your-key-here"

            Or add "ApiKey" to your settings file:
              ~/.sovrant/settings.json
            """;
    }
}
