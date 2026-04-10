using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Api.Capabilities;
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
    public async Task RouteAsync_FallsBackToUnhealthyProviderWhenAllFail()
    {
        // When all providers fail the startup ping, the router falls back to the configured
        // (but unhealthy) providers so a transient DNS hiccup doesn't hard-block routing.
        var info = new ProviderInfo(
            CreateProvider("p1", "https://dead.example.com"), "/v1/models", 0.001) { Healthy = false };

        var router = BuildRouter([info]);
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("Hi")]);

        // Should fall back to the unhealthy provider rather than throwing
        var provider = await router.RouteAsync(req);
        Assert.Equal("p1", provider.Name);
    }

    [Fact]
    public async Task RouteAsync_ThrowsWhenNoProvidersConfigured()
    {
        var router = BuildRouter([]);
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

    [Fact]
    public async Task PinProviderAsync_RoutesToPinnedProvider_IgnoringScore()
    {
        var fast = CreateProvider("fast", "https://fast.example.com");
        var slow = CreateProvider("slow", "https://slow.example.com");
        var fastInfo = new ProviderInfo(fast, "/v1/models", 0.001) { AvgLatencyMs = 1 };
        var slowInfo = new ProviderInfo(slow, "/v1/models", 0.001) { AvgLatencyMs = 999 };
        var router = BuildRouter([fastInfo, slowInfo], RouterMode.Smart, RouterStrategy.Latency);
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("Hi")]);

        await router.PinProviderAsync("slow");
        var selected = await router.RouteAsync(req);

        Assert.Equal("slow", selected.Name);
    }

    [Fact]
    public async Task PinProviderAsync_NullUnpins_ResumesNormalRouting()
    {
        var fast = CreateProvider("fast", "https://fast.example.com");
        var slow = CreateProvider("slow", "https://slow.example.com");
        var fastInfo = new ProviderInfo(fast, "/v1/models", 0.001) { AvgLatencyMs = 1 };
        var slowInfo = new ProviderInfo(slow, "/v1/models", 0.001) { AvgLatencyMs = 999 };
        var router = BuildRouter([fastInfo, slowInfo], RouterMode.Smart, RouterStrategy.Latency);
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("Hi")]);

        await router.PinProviderAsync("slow");
        await router.PinProviderAsync(null); // unpin
        var selected = await router.RouteAsync(req);

        Assert.Equal("fast", selected.Name);
    }

    [Fact]
    public async Task PinProviderAsync_ThrowsForUnrecognisedName()
    {
        var info = new ProviderInfo(CreateProvider("p1", "https://p1.example.com"), "/v1/models", 0.001);
        var router = BuildRouter([info]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.PinProviderAsync("unknown"));
    }

    [Fact]
    public async Task RouteAsync_NormalizesModelId_WhenCapabilityRegistryProvided()
    {
        var registry = new ModelCapabilityRegistry(NullLogger<ModelCapabilityRegistry>.Instance);
        registry.RegisterAlias("gemma4:27b", "google/gemma-4-27b");

        var provider = CreateProvider("p1", "https://p1.example.com");
        var info = new ProviderInfo(provider, "/v1/models", 0.001);
        var pingHttp = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")));
        var router = new SmartRouter([info], RouterMode.Smart, RouterStrategy.Balanced,
            pingHttp, NullLogger<SmartRouter>.Instance, registry);

        var req = new MessagesRequest("gemma4:27b", 100, [InputMessage.UserText("Hi")]);
        var selected = await router.RouteAsync(req);

        // Router should resolve — the key thing is it doesn't throw
        Assert.Equal("p1", selected.Name);
    }

    // ── Intent routing integration tests ─────────────────────────────────

    [Fact]
    public async Task RouteWithIntentAsync_ClassifiesIntent_WhenEnabled()
    {
        var registry = new ModelCapabilityRegistry(NullLogger<ModelCapabilityRegistry>.Instance);
        registry.Register("cheap-model", new ModelCapabilities
        {
            TierHint = ModelTier.Fast,
            CostPerMillionInput = 0.1m,
            Source = CapabilitySource.Live,
        });
        registry.Register("expensive-model", new ModelCapabilities
        {
            TierHint = ModelTier.High,
            CostPerMillionInput = 30m,
            Source = CapabilitySource.Live,
        });

        var tierResolver = new ModelTierResolver(registry, NullLogger<ModelTierResolver>.Instance);
        var provider = CreateProvider("p1", "https://p1.example.com");
        var info = new ProviderInfo(provider, "/v1/models", 0.001);
        var pingHttp = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")));
        var router = new SmartRouter([info], RouterMode.Smart, RouterStrategy.Balanced,
            pingHttp, NullLogger<SmartRouter>.Instance, registry, tierResolver);

        // Simple question → should classify as SimpleQa → fast tier
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("what is 2+2?")]);
        var decision = await router.RouteWithIntentAsync(req);

        Assert.NotNull(decision.Classification);
        Assert.Equal(IntentClass.SimpleQa, decision.Classification.Intent);
        Assert.Equal(ModelTier.Fast, decision.Tier);
        Assert.Equal("cheap-model", decision.ResolvedModel);
    }

    [Fact]
    public async Task RouteWithIntentAsync_RoutesRefactorToHighTier()
    {
        var registry = new ModelCapabilityRegistry(NullLogger<ModelCapabilityRegistry>.Instance);
        registry.Register("fast-model", new ModelCapabilities
        {
            TierHint = ModelTier.Fast,
            CostPerMillionInput = 0.1m,
            Source = CapabilitySource.Live,
        });
        registry.Register("high-model", new ModelCapabilities
        {
            TierHint = ModelTier.High,
            CostPerMillionInput = 30m,
            Source = CapabilitySource.Live,
        });

        var tierResolver = new ModelTierResolver(registry, NullLogger<ModelTierResolver>.Instance);
        var provider = CreateProvider("p1", "https://p1.example.com");
        var info = new ProviderInfo(provider, "/v1/models", 0.001);
        var pingHttp = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")));
        var router = new SmartRouter([info], RouterMode.Smart, RouterStrategy.Balanced,
            pingHttp, NullLogger<SmartRouter>.Instance, registry, tierResolver);

        var req = new MessagesRequest("gpt-4o", 100,
            [InputMessage.UserText("refactor this module into smaller classes")]);
        var decision = await router.RouteWithIntentAsync(req);

        Assert.Equal(IntentClass.Refactor, decision.Classification!.Intent);
        Assert.Equal(ModelTier.High, decision.Tier);
        Assert.Equal("high-model", decision.ResolvedModel);
    }

    [Fact]
    public async Task RouteWithIntentAsync_ReturnsNullClassification_WhenDisabled()
    {
        var provider = CreateProvider("p1", "https://p1.example.com");
        var info = new ProviderInfo(provider, "/v1/models", 0.001);
        var pingHttp = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")));
        var config = new RoutingConfig { IntentRouting = false };
        var router = new SmartRouter([info], RouterMode.Smart, RouterStrategy.Balanced,
            pingHttp, NullLogger<SmartRouter>.Instance, routingConfig: config);

        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("refactor everything")]);
        var decision = await router.RouteWithIntentAsync(req);

        Assert.Null(decision.Classification);
        Assert.Null(decision.ResolvedModel);
        Assert.Equal("p1", decision.Provider.Name);
    }

    [Fact]
    public async Task RouteWithIntentAsync_AppliesCustomRules()
    {
        var registry = new ModelCapabilityRegistry(NullLogger<ModelCapabilityRegistry>.Instance);
        registry.Register("fast-model", new ModelCapabilities
        {
            TierHint = ModelTier.Fast,
            CostPerMillionInput = 0.1m,
            Source = CapabilitySource.Live,
        });
        registry.Register("high-model", new ModelCapabilities
        {
            TierHint = ModelTier.High,
            CostPerMillionInput = 30m,
            Source = CapabilitySource.Live,
        });

        var tierResolver = new ModelTierResolver(registry, NullLogger<ModelTierResolver>.Instance);
        var config = new RoutingConfig
        {
            CustomRules = [new CustomRoutingRule { Pattern = "security|CVE", Tier = ModelTier.High }],
        };

        var provider = CreateProvider("p1", "https://p1.example.com");
        var info = new ProviderInfo(provider, "/v1/models", 0.001);
        var pingHttp = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")));
        var router = new SmartRouter([info], RouterMode.Smart, RouterStrategy.Balanced,
            pingHttp, NullLogger<SmartRouter>.Instance, registry, tierResolver, config);

        // "hello" would normally be Conversation/fast, but the CVE rule overrides
        var req = new MessagesRequest("gpt-4o", 100,
            [InputMessage.UserText("check this CVE-2024-1234 vulnerability")]);
        var decision = await router.RouteWithIntentAsync(req);

        Assert.Equal(ModelTier.High, decision.Tier);
        Assert.Equal("high-model", decision.ResolvedModel);
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
