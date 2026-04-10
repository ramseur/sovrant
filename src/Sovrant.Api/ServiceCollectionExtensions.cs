using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Api.Capabilities;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;

namespace Sovrant.Api;

/// <summary>Extension methods for registering Sovrant LLM provider services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all LLM providers, the SmartRouter, and supporting services.
    /// </summary>
    /// <remarks>
    /// Environment variable priority (highest first):
    /// <list type="number">
    ///   <item><description><c>LLM_API_KEY</c> / <c>LLM_BASE_URL</c></description></item>
    ///   <item><description><c>OPENAI_API_KEY</c> / <c>OPENAI_BASE_URL</c></description></item>
    ///   <item><description><c>PROVIDER_API_KEY</c> / <c>PROVIDER_BASE_URL</c></description></item>
    ///   <item><description>Config file values</description></item>
    /// </list>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLlmProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("PROVIDER_API_KEY")
            ?? configuration["Llm:ApiKey"]
            ?? string.Empty;

        var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL")
            ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
            ?? configuration["Llm:BaseUrl"]
            ?? "https://api.openai.com/v1";

        // Ensure trailing slash so relative paths (e.g. "chat/completions") resolve correctly
        // against any base URL, including non-standard paths like Google AI Studio's /v1beta/openai/.
        if (!baseUrl.EndsWith('/')) baseUrl += "/";

        // ProviderApiProvider uses the native messages API format (/v1/messages).
        // Only register it when a dedicated PROVIDER_BASE_URL is explicitly set — otherwise it
        // would share the same base URL as OpenAiCompatProvider and cause 404s on incompatible hosts.
        var providerApiUrl = Environment.GetEnvironmentVariable("PROVIDER_BASE_URL")
            ?? configuration["Llm:ProviderBaseUrl"];
        var providerApiKey = Environment.GetEnvironmentVariable("PROVIDER_API_KEY")
            ?? configuration["Llm:ProviderApiKey"];
        var hasProviderApi = !string.IsNullOrWhiteSpace(providerApiUrl);
        if (hasProviderApi && !providerApiUrl!.EndsWith('/')) providerApiUrl += "/";

        var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
            ?? configuration["Llm:OllamaBaseUrl"]
            ?? "http://localhost:11434/v1";

        if (!ollamaUrl.EndsWith('/')) ollamaUrl += "/";

        var webSearchEnabled = string.Equals(
            Environment.GetEnvironmentVariable("LLM_WEB_SEARCH"), "true", StringComparison.OrdinalIgnoreCase);

        var routerMode = Enum.TryParse<RouterMode>(
            Environment.GetEnvironmentVariable("ROUTER_MODE") ?? configuration["Router:Mode"], true,
            out var rm) ? rm : RouterMode.Smart;

        var routerStrategy = Enum.TryParse<RouterStrategy>(
            Environment.GetEnvironmentVariable("ROUTER_STRATEGY") ?? configuration["Router:Strategy"], true,
            out var rs) ? rs : RouterStrategy.Balanced;

        services.AddSingleton<IAuthProvider>(new ApiKeyAuthProvider(apiKey));

        // Model capability registry (Phase 54) — layered resolution:
        // user overrides > bundled overrides > live metadata > defaults.
        services.AddSingleton<IModelCapabilityRegistry>(sp =>
            new ModelCapabilityRegistry(sp.GetRequiredService<ILogger<ModelCapabilityRegistry>>()));
        services.AddSingleton<ModelOverrideLoader>();
        services.AddHttpClient("ModelMetadataFetcher");
        services.AddSingleton<LiveModelMetadataFetcher>(sp =>
            new LiveModelMetadataFetcher(
                sp.GetRequiredService<IModelCapabilityRegistry>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ModelMetadataFetcher"),
                sp.GetRequiredService<ILogger<LiveModelMetadataFetcher>>()));

        // Providers inject ILogger (non-generic base interface). Register a factory-backed instance
        // so the DI container can resolve it for typed HTTP clients.
        services.AddSingleton<ILogger>(sp =>
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("Sovrant.Api"));

        // When LLM_WEB_SEARCH=true, use the Responses API provider (POST /v1/responses) which
        // supports web_search_preview. Otherwise use the standard chat completions provider.
        if (webSearchEnabled)
            services.AddHttpClient<OpenAiResponsesProvider>(c => c.BaseAddress = new Uri(baseUrl));
        else
            services.AddHttpClient<OpenAiCompatProvider>(c => c.BaseAddress = new Uri(baseUrl));

        services.AddHttpClient<OllamaProvider>(c => c.BaseAddress = new Uri(ollamaUrl));
        if (hasProviderApi)
        {
            services.AddHttpClient<ProviderApiProvider>(c =>
            {
                c.BaseAddress = new Uri(providerApiUrl!);
                c.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                if (!string.IsNullOrWhiteSpace(providerApiKey))
                    c.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("x-api-key", providerApiKey);
            });
        }
        services.AddHttpClient("SmartRouterPing");

        services.AddSingleton<ISmartRouter>(sp =>
        {
            ILlmProvider primaryProvider = webSearchEnabled
                ? sp.GetRequiredService<OpenAiResponsesProvider>()
                : sp.GetRequiredService<OpenAiCompatProvider>();
            var ollamaProv = sp.GetRequiredService<OllamaProvider>();
            var logger = sp.GetRequiredService<ILogger<SmartRouter>>();
            var pingClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("SmartRouterPing");

            var providers = new List<ProviderInfo>
            {
                new(primaryProvider, "models", 0.002),
                new(ollamaProv, "models", 0.0),
            };

            // Only add ProviderApiProvider when explicitly configured.
            if (hasProviderApi)
            {
                var provApiProv = sp.GetRequiredService<ProviderApiProvider>();
                providers.Add(new(provApiProv, "models", 0.003));
            }

            var capRegistry = sp.GetService<IModelCapabilityRegistry>();
            var tierResolver = sp.GetService<IModelTierResolver>();
            var routingConfig = sp.GetService<RoutingConfig>();
            return new SmartRouter(providers, routerMode, routerStrategy, pingClient, logger,
                capRegistry, tierResolver, routingConfig);
        });

        // Phase 48 — intent-aware routing support.
        services.AddSingleton<RoutingConfig>(_ => RoutingConfigLoader.Load());
        services.AddSingleton<IModelTierResolver>(sp =>
        {
            var registry = sp.GetRequiredService<IModelCapabilityRegistry>();
            var tierLogger = sp.GetRequiredService<ILogger<ModelTierResolver>>();
            var config = sp.GetRequiredService<RoutingConfig>();
            return new ModelTierResolver(registry, tierLogger, config.TierModels, config.FreeModelsOnly);
        });

        return services;
    }
}
