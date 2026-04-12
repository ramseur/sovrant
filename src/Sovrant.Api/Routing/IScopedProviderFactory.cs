using Sovrant.Api.Providers;

namespace Sovrant.Api.Routing;

/// <summary>
/// Creates scoped, single-provider routers for per-request provider overrides
/// (e.g., when a client supplies X-LLM-Api-Key and X-LLM-Base-Url headers).
/// </summary>
public interface IScopedProviderFactory
{
    /// <summary>
    /// Creates a <see cref="ISmartRouter"/> that routes all requests to a single
    /// <see cref="OpenAiCompatProvider"/> constructed from the given credentials.
    /// </summary>
    ISmartRouter Create(HttpClient http, string apiKey);
}
