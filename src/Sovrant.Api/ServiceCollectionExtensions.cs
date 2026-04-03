using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
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
            ?? Environment.GetEnvironmentVariable("PROVIDER_BASE_URL")
            ?? configuration["Llm:BaseUrl"]
            ?? "https://api.openai.com/v1";

        var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
            ?? configuration["Llm:OllamaBaseUrl"]
            ?? "http://localhost:11434";

        var routerMode = Enum.TryParse<RouterMode>(
            Environment.GetEnvironmentVariable("ROUTER_MODE") ?? configuration["Router:Mode"], true,
            out var rm) ? rm : RouterMode.Smart;

        var routerStrategy = Enum.TryParse<RouterStrategy>(
            Environment.GetEnvironmentVariable("ROUTER_STRATEGY") ?? configuration["Router:Strategy"], true,
            out var rs) ? rs : RouterStrategy.Balanced;

        services.AddSingleton<IAuthProvider>(new ApiKeyAuthProvider(apiKey));

        services.AddHttpClient<OpenAiCompatProvider>(c => c.BaseAddress = new Uri(baseUrl));
        services.AddHttpClient<OllamaProvider>(c => c.BaseAddress = new Uri(ollamaUrl));
        services.AddHttpClient<ProviderApiProvider>(c =>
        {
            c.BaseAddress = new Uri(baseUrl);
            c.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });
        services.AddHttpClient("SmartRouterPing");

        services.AddSingleton<ISmartRouter>(sp =>
        {
            var openAiProv = sp.GetRequiredService<OpenAiCompatProvider>();
            var ollamaProv = sp.GetRequiredService<OllamaProvider>();
            var provApiProv = sp.GetRequiredService<ProviderApiProvider>();
            var logger = sp.GetRequiredService<ILogger<SmartRouter>>();
            var pingClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("SmartRouterPing");

            var providers = new List<ProviderInfo>
            {
                new(openAiProv, "/v1/models", 0.002),
                new(ollamaProv, "/api/tags", 0.0),
                new(provApiProv, "/v1/models", 0.003),
            };

            return new SmartRouter(providers, routerMode, routerStrategy, pingClient, logger);
        });

        return services;
    }
}
