using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;

namespace Sovrant.Api.Tests;

/// <summary>Tests for <see cref="ScopedSingleProviderRouter"/>.</summary>
public sealed class ScopedSingleProviderRouterTests
{
    private static OpenAiCompatProvider CreateProvider(string name, string baseUrl)
    {
        var http = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")))
        {
            BaseAddress = new Uri(baseUrl)
        };
        return new NamedProvider(http, new ApiKeyAuthProvider("test-key"), NullLogger.Instance, name);
    }

    private sealed class NamedProvider(
        HttpClient http, IAuthProvider auth, Microsoft.Extensions.Logging.ILogger logger, string name)
        : OpenAiCompatProvider(http, auth, logger)
    {
        public override string Name => name;
    }

    [Fact]
    public async Task RouteAsync_AlwaysReturnsSameProvider()
    {
        var provider = CreateProvider("scoped-openai", "https://api.openai.com/v1/");
        var router = new ScopedSingleProviderRouter(provider);

        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("Hi")]);

        var routed1 = await router.RouteAsync(req);
        var routed2 = await router.RouteAsync(req);

        Assert.Same(provider, routed1);
        Assert.Same(provider, routed2);
    }

    [Fact]
    public async Task InitializeAsync_IsNoOp()
    {
        var provider = CreateProvider("test", "https://example.com/v1/");
        var router = new ScopedSingleProviderRouter(provider);

        // Should not throw or block.
        await router.InitializeAsync();
    }

    [Fact]
    public void GetStatus_ReturnsSingleEntry()
    {
        var provider = CreateProvider("my-provider", "https://example.com/v1/");
        var router = new ScopedSingleProviderRouter(provider);

        var status = router.GetStatus();

        Assert.Single(status);
        Assert.Equal("my-provider", status[0].Name);
        Assert.True(status[0].Healthy);
        Assert.Equal("scoped", status[0].Score);
    }

    [Fact]
    public async Task RecordResultAsync_IsNoOp()
    {
        var provider = CreateProvider("test", "https://example.com/v1/");
        var router = new ScopedSingleProviderRouter(provider);

        // Should not throw or block.
        await router.RecordResultAsync("test", true, 42.0);
    }

    [Fact]
    public async Task PinProviderAsync_IsNoOp()
    {
        var provider = CreateProvider("test", "https://example.com/v1/");
        var router = new ScopedSingleProviderRouter(provider);

        // Should not throw — pin is meaningless for single-provider router.
        await router.PinProviderAsync("anything");
        await router.PinProviderAsync(null);
    }
}
