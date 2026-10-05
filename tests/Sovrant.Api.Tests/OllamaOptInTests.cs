using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Api.Config;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;

namespace Sovrant.Api.Tests;

/// <summary>
/// Phase 138 — Sovrant never contacts Ollama unless an Ollama provider is enabled for the
/// workspace. There is no always-on Ollama provider at a default localhost:11434, and every
/// profile (cloud or local) is served by the primary provider at the profile's base URL.
/// </summary>
public sealed class OllamaOptInTests
{
    private static ServiceProvider BuildApiServices()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLlmProviders(configuration, CredentialConfig.Resolve(configuration));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Router_Has_No_Ollama_Provider()
    {
        using var sp = BuildApiServices();
        var router = sp.GetRequiredService<ISmartRouter>();

        var names = router.GetStatus().Select(s => s.Name).ToList();
        Assert.DoesNotContain("ollama", names);
        Assert.Equal("openai-compat", names[0]);
    }

    [Fact]
    public async Task Pinning_Ollama_Explains_It_Must_Be_Enabled_For_The_Workspace()
    {
        using var sp = BuildApiServices();
        var router = sp.GetRequiredService<ISmartRouter>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => router.PinProviderAsync("ollama"));
        Assert.Contains("isn't enabled for this workspace", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PinActiveProfile_Pins_The_Primary_Provider()
    {
        var http = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")));
        var primary = new SmartRouterTests.NamedOpenAiCompatProvider(new HttpClient { BaseAddress = new Uri("https://api.openai.com/v1/") },
            new ApiKeyAuthProvider("k"), NullLogger.Instance, "openai-responses");
        var secondary = new SmartRouterTests.NamedOpenAiCompatProvider(new HttpClient { BaseAddress = new Uri("https://api.anthropic.com/") },
            new ApiKeyAuthProvider("k"), NullLogger.Instance, "provider-api");
        var router = new SmartRouter([new(primary, "models", 0.002), new(secondary, "models", 0.0)],
            new RouterOptions(), http, NullLogger<SmartRouter>.Instance);

        await router.PinActiveProfileAsync();

        var routed = await router.RouteAsync(new Sovrant.Api.Types.MessagesRequest("m", 100, []));
        Assert.Equal("openai-responses", routed.Name); // the cheaper secondary never wins over the pin
    }

    [Fact]
    public async Task PinActiveProfile_Is_A_NoOp_With_No_Providers()
    {
        var router = new SmartRouter([], new RouterOptions(),
            new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}"))), NullLogger<SmartRouter>.Instance);
        await router.PinActiveProfileAsync(); // does not throw
    }

    [Fact]
    public void OllamaBaseUrl_Has_No_Default()
    {
        var previous = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("OLLAMA_BASE_URL", null);
            var credentials = CredentialConfig.Resolve(new ConfigurationBuilder().AddInMemoryCollection().Build());
            Assert.Null(credentials.OllamaBaseUrl);
            Assert.Equal("http://localhost:11434/v1", CredentialConfig.OllamaPrefillBaseUrl());

            Environment.SetEnvironmentVariable("OLLAMA_BASE_URL", "http://gpu-box:11434/v1/");
            Assert.Equal("http://gpu-box:11434/v1", CredentialConfig.OllamaPrefillBaseUrl());
        }
        finally
        {
            Environment.SetEnvironmentVariable("OLLAMA_BASE_URL", previous);
        }
    }

    [Theory]
    [InlineData("Ollama", true)]
    [InlineData("LM Studio", true)]
    [InlineData("OpenRouter", false)]
    [InlineData(null, false)]
    public void Local_Providers_Need_No_Key(string? kind, bool local)
    {
        Assert.Equal(local, CredentialConfig.IsLocalProvider(kind));
    }
}
