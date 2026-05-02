using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Api.Capabilities;
using Sovrant.Api.Providers;
using Sovrant.Api.Config;
using Sovrant.Api.Routing;

namespace Sovrant.Api;

/// <summary>Extension methods for registering Sovrant LLM provider services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all LLM providers, the SmartRouter, and supporting services
    /// using a pre-resolved <see cref="CredentialConfig"/>.
    /// </summary>
    public static IServiceCollection AddLlmProviders(
        this IServiceCollection services,
        IConfiguration configuration,
        CredentialConfig credentials)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(credentials);

        var apiKey = credentials.LlmApiKey;
        var baseUrl = credentials.LlmBaseUrl;
        var providerApiUrl = credentials.ProviderBaseUrl;
        var hasProviderApi = credentials.HasProviderApi;
        var ollamaUrl = credentials.OllamaBaseUrl;

        // Web search backend selection (Phase 70). Registered as singleton so providers
        // and the WebSearchTool can consult the same resolved value. Until PR 3 lands,
        // OpenAiResponsesProvider continues to be selected by the legacy
        // CredentialConfig.WebSearchEnabled flag for back-compat — both the legacy
        // env var and the new SOVRANT_WEB_SEARCH=native feed into that flag below.
        services.AddSingleton(sp => WebSearchOptions.Resolve(
            configuration, sp.GetService<ILogger<WebSearchOptions>>()));

        var webSearchEnabled = credentials.WebSearchEnabled
            || (Environment.GetEnvironmentVariable("SOVRANT_WEB_SEARCH")?.Trim()
                .Equals("native", StringComparison.OrdinalIgnoreCase) ?? false);

        // Live-mutable router options (Phase 70+). Singleton so the Settings UI can
        // hot-swap Mode / Strategy at runtime; SmartRouter consults this on each route.
        services.AddSingleton(_ => RouterOptions.Resolve(configuration));

        services.AddSingleton<IAuthProvider>(new ApiKeyAuthProvider(apiKey));

        // Bootstrap-time API-key resolver — env > snapshot. Sovrant.Runtime
        // overrides this with a credential-store-aware adapter once the encrypted
        // store is open (see CredentialStoreApiKeyResolver).
        services.AddSingleton<IApiKeyResolver, EnvApiKeyResolver>();

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
                sp.GetRequiredService<ILogger<LiveModelMetadataFetcher>>(),
                sp.GetService<CredentialConfig>(),
                sp.GetService<IApiKeyResolver>()));

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
            // Bucket-C: x-api-key is now resolved per-request inside BuildRequestAsync via
            // IApiKeyResolver (env > store > snapshot), so we no longer bake the snapshot
            // value into the HttpClient default headers — that path missed runtime rotations
            // and used a malformed Authorization scheme.
            services.AddHttpClient<ProviderApiProvider>(c =>
            {
                c.BaseAddress = new Uri(providerApiUrl!);
                c.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            });
        }
        services.AddHttpClient("SmartRouterPing");

        // Pass the credentials snapshot as a singleton so providers can ask
        // the centralised NativeWebSearchInjector whether Brave / FireCrawl
        // keys are present without poking environment variables themselves.
        services.AddSingleton(credentials);

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
            var routerOptions = sp.GetRequiredService<RouterOptions>();
            return new SmartRouter(providers, routerOptions, pingClient, logger,
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

        services.AddSingleton<IScopedProviderFactory, DefaultScopedProviderFactory>();

        return services;
    }
}
