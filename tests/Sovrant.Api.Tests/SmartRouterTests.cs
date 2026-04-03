using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;

namespace Sovrant.Api.Tests;

/// <summary>Tests for <see cref="SmartRouter"/>.</summary>
public sealed class SmartRouterTests
{
    private static OpenAiCompatProvider CreateProvider(string name, string baseUrl)
    {
        var http = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")))
        {
            BaseAddress = new Uri(baseUrl)
        };
        return new NamedOpenAiCompatProvider(http, new ApiKeyAuthProvider("test"), NullLogger.Instance, name);
    }

    private static SmartRouter BuildRouter(
        IReadOnlyList<ProviderInfo> providers,
        RouterMode mode = RouterMode.Smart,
        RouterStrategy strategy = RouterStrategy.Balanced)
    {
        var pingHttp = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")));
        return new SmartRouter(providers, mode, strategy, pingHttp, NullLogger<SmartRouter>.Instance);
    }

    [Fact]
    public async Task RouteAsync_PicksLowestScoringProvider_WhenSmartMode()
    {
        var fastProvider = CreateProvider("fast", "https://fast.example.com");
        var slowProvider = CreateProvider("slow", "https://slow.example.com");

        var fastInfo = new ProviderInfo(fastProvider, "/v1/models", 0.001) { AvgLatencyMs = 100 };
        var slowInfo = new ProviderInfo(slowProvider, "/v1/models", 0.001) { AvgLatencyMs = 900 };

        var router = BuildRouter([fastInfo, slowInfo], RouterMode.Smart, RouterStrategy.Latency);
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("Hi")]);

        var selected = await router.RouteAsync(req);
        Assert.Equal("fast", selected.Name);
    }

    [Fact]
    public async Task RouteAsync_PicksFirst_WhenFixedMode()
    {
        var first = CreateProvider("first", "https://first.example.com");
        var second = CreateProvider("second", "https://second.example.com");
        var firstInfo = new ProviderInfo(first, "/v1/models", 0.001) { AvgLatencyMs = 999 };
        var secondInfo = new ProviderInfo(second, "/v1/models", 0.001) { AvgLatencyMs = 1 };

        var router = BuildRouter([firstInfo, secondInfo], RouterMode.Fixed);
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("Hi")]);

        var selected = await router.RouteAsync(req);
        Assert.Equal("first", selected.Name);
    }

    [Fact]
    public async Task RouteAsync_ThrowsWhenNoHealthyProviders()
    {
        var info = new ProviderInfo(
            CreateProvider("p1", "https://dead.example.com"), "/v1/models", 0.001) { Healthy = false };

        var router = BuildRouter([info]);
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("Hi")]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.RouteAsync(req));
    }

    [Fact]
    public async Task RecordResultAsync_UpdatesAvgLatency_OnSuccess()
    {
        var info = new ProviderInfo(CreateProvider("p1", "https://p1.example.com"), "/v1/models", 0.001)
        {
            AvgLatencyMs = 1000
        };
        var router = BuildRouter([info]);

        await router.RecordResultAsync("p1", success: true, durationMs: 200);

        // EMA: 0.3 * 200 + 0.7 * 1000 = 760
        Assert.InRange(info.AvgLatencyMs, 759, 761);
        Assert.Equal(1, info.RequestCount);
    }

    [Fact]
    public async Task RecordResultAsync_MarksUnhealthy_AfterHighErrorRate()
    {
        var info = new ProviderInfo(CreateProvider("p1", "https://p1.example.com"), "/v1/models", 0.001);
        var router = BuildRouter([info]);

        // 3 requests, 3 errors → error rate = 100%
        await router.RecordResultAsync("p1", success: false, durationMs: 0);
        await router.RecordResultAsync("p1", success: false, durationMs: 0);
        await router.RecordResultAsync("p1", success: false, durationMs: 0);

        Assert.False(info.Healthy);
    }

    [Fact]
    public void GetStatus_ReturnsOneEntryPerProvider()
    {
        var providers = new List<ProviderInfo>
        {
            new(CreateProvider("a", "https://a.example.com"), "/v1/models", 0.001),
            new(CreateProvider("b", "https://b.example.com"), "/v1/models", 0.002),
        };
        var router = BuildRouter(providers);

        var status = router.GetStatus();

        Assert.Equal(2, status.Count);
        Assert.Contains(status, s => s.Name == "a");
        Assert.Contains(status, s => s.Name == "b");
    }

    /// <summary>Helper subclass that overrides Name for testing.</summary>
    private sealed class NamedOpenAiCompatProvider : OpenAiCompatProvider
    {
        private readonly string _name;
        public NamedOpenAiCompatProvider(HttpClient http, IAuthProvider auth,
            Microsoft.Extensions.Logging.ILogger logger, string name) : base(http, auth, logger) => _name = name;
        public override string Name => _name;
    }
}
