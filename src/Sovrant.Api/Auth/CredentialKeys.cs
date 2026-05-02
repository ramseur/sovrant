namespace Sovrant.Api.Auth;

/// <summary>
/// Well-known keys for <see cref="Sovrant.Runtime.Mcp.ICredentialStore"/> entries holding
/// provider API keys. Bucket-C of the config audit (<c>docs/config-audit.md</c>) — these
/// values used to live only in environment variables. They now live in the encrypted
/// credential store, with env vars retained as runtime overrides for 12-factor parity.
/// </summary>
/// <remarks>
/// Keys are namespaced under <c>llm.</c>, <c>provider.</c>, <c>websearch.</c>, etc., so they
/// don't collide with MCP credentials (<c>mcp.{name}.client_secret</c>) or future stores.
/// </remarks>
public static class CredentialKeys
{
    /// <summary>Primary LLM API key. Env override: <c>LLM_API_KEY</c> &gt; <c>OPENAI_API_KEY</c>.</summary>
    public const string LlmApiKey = "llm.api_key";

    /// <summary>Anthropic-style native messages API key. Env override: <c>PROVIDER_API_KEY</c>.</summary>
    public const string ProviderApiKey = "provider.api_key";

    /// <summary>OpenRouter API key for live model metadata. Env override: <c>OPENROUTER_API_KEY</c>.</summary>
    public const string OpenRouterApiKey = "openrouter.api_key";

    /// <summary>Brave Search API key. Env override: <c>BRAVE_API_KEY</c>.</summary>
    public const string BraveApiKey = "websearch.brave_api_key";

    /// <summary>FireCrawl API key. Env override: <c>FIRECRAWL_API_KEY</c>.</summary>
    public const string FirecrawlApiKey = "websearch.firecrawl_api_key";
}
