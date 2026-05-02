using Microsoft.Extensions.Configuration;

namespace Sovrant.Api.Config;

/// <summary>
/// Centralised, read-once snapshot of all external credentials.
/// Resolves the multi-source priority chain (env var > config file > default) exactly once
/// at startup so that no consumer needs to repeat the look-up logic.
/// </summary>
public sealed class CredentialConfig
{
    /// <summary>Primary LLM API key (LLM_API_KEY > OPENAI_API_KEY > PROVIDER_API_KEY > config).</summary>
    public string LlmApiKey { get; init; } = string.Empty;

    /// <summary>Primary LLM base URL (LLM_BASE_URL > OPENAI_BASE_URL > config). Trailing-slash normalised.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String form used throughout for trailing-slash normalisation and HttpClient.BaseAddress construction.")]
    public string LlmBaseUrl { get; init; } = "https://api.openai.com/v1";

    /// <summary>Anthropic-style provider API key (PROVIDER_API_KEY > config).</summary>
    public string ProviderApiKey { get; init; } = string.Empty;

    /// <summary>Anthropic-style provider base URL (PROVIDER_BASE_URL > config). Empty means not configured.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String form used throughout for trailing-slash normalisation and HttpClient.BaseAddress construction.")]
    public string ProviderBaseUrl { get; init; } = string.Empty;

    /// <summary>Ollama base URL (OLLAMA_BASE_URL > config).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String form used throughout for trailing-slash normalisation and HttpClient.BaseAddress construction.")]
    public string OllamaBaseUrl { get; init; } = "http://localhost:11434/v1";

    /// <summary>Brave Search API key (BRAVE_API_KEY).</summary>
    public string BraveApiKey { get; init; } = string.Empty;

    /// <summary>FireCrawl API key (FIRECRAWL_API_KEY).</summary>
    public string FirecrawlApiKey { get; init; } = string.Empty;

    /// <summary>OpenRouter API key for model metadata fetching (OPENROUTER_API_KEY).</summary>
    public string OpenRouterApiKey { get; init; } = string.Empty;

    /// <summary>Whether web search via the Responses API is enabled (LLM_WEB_SEARCH=true).</summary>
    public bool WebSearchEnabled { get; init; }

    /// <summary>Whether a dedicated Anthropic-style provider endpoint is configured.</summary>
    public bool HasProviderApi => !string.IsNullOrWhiteSpace(ProviderBaseUrl);

    /// <summary>
    /// Builds a <see cref="CredentialConfig"/> by resolving the standard priority chain:
    /// environment variables > <see cref="IConfiguration"/> values > defaults.
    /// </summary>
    public static CredentialConfig Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var llmApiKey = Environment.GetEnvironmentVariable("LLM_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("PROVIDER_API_KEY")
            ?? configuration["Llm:ApiKey"]
            ?? configuration["ApiKey"]
            ?? string.Empty;

        var llmBaseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL")
            ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
            ?? configuration["Llm:BaseUrl"]
            ?? configuration["BaseUrl"]
            ?? "https://api.openai.com/v1";

        if (!llmBaseUrl.EndsWith('/')) llmBaseUrl += "/";

        var providerBaseUrl = Environment.GetEnvironmentVariable("PROVIDER_BASE_URL")
            ?? configuration["Llm:ProviderBaseUrl"]
            ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(providerBaseUrl) && !providerBaseUrl.EndsWith('/'))
            providerBaseUrl += "/";

        var providerApiKey = Environment.GetEnvironmentVariable("PROVIDER_API_KEY")
            ?? configuration["Llm:ProviderApiKey"]
            ?? string.Empty;

        var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
            ?? configuration["Llm:OllamaBaseUrl"]
            ?? "http://localhost:11434/v1";
        if (!ollamaUrl.EndsWith('/')) ollamaUrl += "/";

        return new CredentialConfig
        {
            LlmApiKey = llmApiKey,
            LlmBaseUrl = llmBaseUrl,
            ProviderApiKey = providerApiKey,
            ProviderBaseUrl = providerBaseUrl,
            OllamaBaseUrl = ollamaUrl,
            BraveApiKey = Environment.GetEnvironmentVariable("BRAVE_API_KEY") ?? string.Empty,
            FirecrawlApiKey = Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY") ?? string.Empty,
            OpenRouterApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? string.Empty,
            WebSearchEnabled = string.Equals(
                Environment.GetEnvironmentVariable("LLM_WEB_SEARCH"), "true", StringComparison.OrdinalIgnoreCase),
        };
    }
}
