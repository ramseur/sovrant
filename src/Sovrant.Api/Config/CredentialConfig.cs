using Microsoft.Extensions.Configuration;

namespace Sovrant.Api.Config;

/// <summary>
/// Centralised bootstrap snapshot of structural configuration (base URLs, feature flags).
/// API keys are never read from environment variables — they live exclusively in the
/// encrypted <see cref="Sovrant.Runtime.Mcp.ICredentialStore"/> (SQLite, AES-256-GCM)
/// and are resolved at request time by <c>CredentialStoreAuthProvider</c>.
/// </summary>
public sealed class CredentialConfig
{
    /// <summary>
    /// Primary LLM API key — always empty in this snapshot.
    /// Actual key is resolved at request time from the credential store.
    /// </summary>
    public string LlmApiKey { get; init; } = string.Empty;

    /// <summary>Bootstrap default base URL for the HttpClient used before the credential store loads.
    /// The running value is overridden per-request by <c>MutableAuthProvider.BaseUrl</c>.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String form used throughout for trailing-slash normalisation and HttpClient.BaseAddress construction.")]
    public string LlmBaseUrl { get; init; } = "https://api.openai.com/v1/";

    /// <summary>Anthropic-style provider API key — always empty; credential store only.</summary>
    public string ProviderApiKey { get; init; } = string.Empty;

    /// <summary>Anthropic-style provider base URL (PROVIDER_BASE_URL env var — infrastructure config, not a secret).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String form used throughout for trailing-slash normalisation and HttpClient.BaseAddress construction.")]
    public string ProviderBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Ollama address from <c>OLLAMA_BASE_URL</c> (shell or <c>.env</c>) or <c>Llm:OllamaBaseUrl</c>;
    /// <see langword="null"/> when unset. Phase 138: this only pre-fills the base URL of a new
    /// Ollama provider profile. On its own it never makes Sovrant contact Ollama; that needs an
    /// Ollama profile enabled for the workspace.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String form used throughout for trailing-slash normalisation and HttpClient.BaseAddress construction.")]
    public string? OllamaBaseUrl { get; init; }

    /// <summary>
    /// The address an admin's new Ollama provider is pre-filled with: <c>OLLAMA_BASE_URL</c> (shell
    /// or <c>.env</c>) when set, else Ollama's standard local address. Only text in a form field;
    /// it never makes Sovrant contact Ollama (Phase 138).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "Pre-fills a text field and is stored as a string on the profile.")]
    public static string OllamaPrefillBaseUrl() =>
        (Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") is { Length: > 0 } configured
            ? configured
            : "http://localhost:11434/v1").Trim().TrimEnd('/');

    /// <summary>Providers that run locally and need no API key.</summary>
    public static bool IsLocalProvider(string? providerKind) => providerKind is "Ollama" or "LM Studio";

    /// <summary>Brave Search API key — always empty; credential store only.</summary>
    public string BraveApiKey { get; init; } = string.Empty;

    /// <summary>FireCrawl API key — always empty; credential store only.</summary>
    public string FirecrawlApiKey { get; init; } = string.Empty;

    /// <summary>OpenRouter API key — always empty; credential store only.</summary>
    public string OpenRouterApiKey { get; init; } = string.Empty;

    /// <summary>Whether web search via the Responses API is enabled (SOVRANT_WEB_SEARCH env var).</summary>
    public bool WebSearchEnabled { get; init; }

    /// <summary>Whether a dedicated Anthropic-style provider endpoint is configured.</summary>
    public bool HasProviderApi => !string.IsNullOrWhiteSpace(ProviderBaseUrl);

    /// <summary>
    /// Builds a <see cref="CredentialConfig"/> from structural environment variables only.
    /// No API keys are read from the environment — they live in the credential store.
    /// </summary>
    public static CredentialConfig Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Infrastructure endpoints (not secrets — these are service addresses, not API keys).
        var providerBaseUrl = Environment.GetEnvironmentVariable("PROVIDER_BASE_URL")
            ?? configuration["Llm:ProviderBaseUrl"]
            ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(providerBaseUrl) && !providerBaseUrl.EndsWith('/'))
            providerBaseUrl += "/";

        // Phase 138: no default. An unset address means "no Ollama pre-fill", never "contact localhost:11434".
        var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
            ?? configuration["Llm:OllamaBaseUrl"];
        if (string.IsNullOrWhiteSpace(ollamaUrl))
            ollamaUrl = null;
        else if (!ollamaUrl.EndsWith('/'))
            ollamaUrl += "/";

        return new CredentialConfig
        {
            LlmBaseUrl = "https://api.openai.com/v1/",
            ProviderBaseUrl = providerBaseUrl,
            OllamaBaseUrl = ollamaUrl,
            WebSearchEnabled = string.Equals(
                Environment.GetEnvironmentVariable("SOVRANT_WEB_SEARCH"), "native", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                Environment.GetEnvironmentVariable("LLM_WEB_SEARCH"), "true", StringComparison.OrdinalIgnoreCase),
        };
    }
}
