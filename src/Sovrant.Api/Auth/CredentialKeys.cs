namespace Sovrant.Api.Auth;

/// <summary>
/// Well-known keys for <see cref="Sovrant.Runtime.Mcp.ICredentialStore"/> entries.
/// All API keys live exclusively in the AES-256-GCM encrypted credential store —
/// never in environment variables or config files.
/// </summary>
/// <remarks>
/// Keys are namespaced under <c>llm.</c>, <c>provider.</c>, <c>websearch.</c>, etc.,
/// so they don't collide with MCP credentials (<c>mcp.{name}.client_secret</c>).
/// Per-profile keys use the pattern <c>provider.{profileId}.api_key</c>.
/// </remarks>
public static class CredentialKeys
{
    /// <summary>Primary LLM API key (global fallback for installs without a provider profile).</summary>
    public const string LlmApiKey = "llm.api_key";

    /// <summary>Anthropic-style native messages API key.</summary>
    public const string ProviderApiKey = "provider.api_key";

    /// <summary>OpenRouter API key for live model metadata fetching.</summary>
    public const string OpenRouterApiKey = "openrouter.api_key";

    /// <summary>Brave Search API key.</summary>
    public const string BraveApiKey = "websearch.brave_api_key";

    /// <summary>FireCrawl API key.</summary>
    public const string FirecrawlApiKey = "websearch.firecrawl_api_key";

    /// <summary>Runtime mode: "local" (embedded) or "remote" (connect to Sovrant.Server).</summary>
    public const string RuntimeMode = "sovrant.runtime_mode";

    /// <summary>Base URL of the remote Sovrant server (e.g. <c>http://my-server:5200</c>).</summary>
    public const string RemoteServerUrl = "sovrant.remote.server_url";

    /// <summary>Bearer token for authenticating with the remote server.</summary>
    public const string RemoteApiToken = "sovrant.remote.api_token";
}
