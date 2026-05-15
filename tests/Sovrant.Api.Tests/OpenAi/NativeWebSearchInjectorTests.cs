using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Capabilities;
using Sovrant.Api.Config;
using Sovrant.Api.OpenAi;

namespace Sovrant.Api.Tests.OpenAi;

/// <summary>Tests for <see cref="NativeWebSearchInjector"/>.</summary>
public sealed class NativeWebSearchInjectorTests
{
    private readonly ModelCapabilityRegistry _registry = new(NullLogger<ModelCapabilityRegistry>.Instance);

    public NativeWebSearchInjectorTests()
    {
        // Register one model that supports native and one that doesn't.
        _registry.Register("model-with-native", new ModelCapabilities { SupportsNativeWebSearch = true });
        _registry.Register("model-without-native", new ModelCapabilities { SupportsNativeWebSearch = false });
    }

    private static WebSearchOptions Opts(WebSearchBackend backend) => new() { Backend = backend };

    [Fact]
    public void Plan_Off_SuppressesFunctionToolAndDoesNotInject()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.Off), _registry, hasBraveKey: false, hasFirecrawlKey: false);

        Assert.False(plan.InjectNative);
        Assert.True(plan.SuppressFunctionTool);
    }

    [Fact]
    public void Plan_Brave_KeepsFunctionToolActiveAndDoesNotInjectNative()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.Brave), _registry, hasBraveKey: true, hasFirecrawlKey: false);

        Assert.False(plan.InjectNative);
        Assert.False(plan.SuppressFunctionTool);
    }

    [Fact]
    public void Plan_Firecrawl_KeepsFunctionToolActiveAndDoesNotInjectNative()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.Firecrawl), _registry, hasBraveKey: false, hasFirecrawlKey: true);

        Assert.False(plan.InjectNative);
        Assert.False(plan.SuppressFunctionTool);
    }

    [Fact]
    public void Plan_Native_AlwaysInjectsAndSuppressesFunctionTool_EvenWhenModelUnsupported()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-without-native", Opts(WebSearchBackend.Native), _registry, hasBraveKey: false, hasFirecrawlKey: false);

        Assert.True(plan.InjectNative);
        Assert.True(plan.SuppressFunctionTool);
        Assert.Contains("unsupported", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_Native_OnSupportedModel_HasSupportedReason()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.Native), _registry, hasBraveKey: false, hasFirecrawlKey: false);

        Assert.True(plan.InjectNative);
        Assert.True(plan.SuppressFunctionTool);
        Assert.DoesNotContain("unsupported", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_Auto_BraveKeyPresent_PrefersBrave()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.Auto), _registry, hasBraveKey: true, hasFirecrawlKey: false);

        Assert.False(plan.InjectNative);
        Assert.False(plan.SuppressFunctionTool);
        Assert.Contains("brave", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_Auto_FirecrawlKeyPresent_PrefersFirecrawl()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.Auto), _registry, hasBraveKey: false, hasFirecrawlKey: true);

        Assert.False(plan.InjectNative);
        Assert.False(plan.SuppressFunctionTool);
        Assert.Contains("firecrawl", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_Auto_BothKeysPresent_PrefersBraveOverFirecrawl()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.Auto), _registry, hasBraveKey: true, hasFirecrawlKey: true);

        Assert.False(plan.InjectNative);
        Assert.Contains("brave", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_Auto_NoKeys_ModelSupportsNative_InjectsNative()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.Auto), _registry, hasBraveKey: false, hasFirecrawlKey: false);

        Assert.True(plan.InjectNative);
        Assert.True(plan.SuppressFunctionTool);
    }

    [Fact]
    public void Plan_Auto_NoKeys_ModelLacksNative_NoProvider()
    {
        var plan = NativeWebSearchInjector.Plan(
            "model-without-native", Opts(WebSearchBackend.Auto), _registry, hasBraveKey: false, hasFirecrawlKey: false);

        Assert.False(plan.InjectNative);
        Assert.False(plan.SuppressFunctionTool);
        Assert.Contains("no provider", plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_SearxngFuture_BehavesLikeAuto()
    {
        var planAuto = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.Auto), _registry, false, false);
        var planFuture = NativeWebSearchInjector.Plan(
            "model-with-native", Opts(WebSearchBackend.SearxngFuture), _registry, false, false);

        Assert.Equal(planAuto.InjectNative, planFuture.InjectNative);
        Assert.Equal(planAuto.SuppressFunctionTool, planFuture.SuppressFunctionTool);
    }

    [Fact]
    public void Plan_NullCapabilities_TreatsModelAsUnsupported()
    {
        var plan = NativeWebSearchInjector.Plan(
            "any-model", Opts(WebSearchBackend.Auto), capabilities: null, hasBraveKey: false, hasFirecrawlKey: false);

        Assert.False(plan.InjectNative);
        Assert.Contains("no provider", plan.Reason, StringComparison.Ordinal);
    }
}

/// <summary>Tests for <see cref="OpenAiDialectResolver"/>.</summary>
public sealed class OpenAiDialectResolverTests
{
    [Theory]
    [InlineData("https://api.openai.com/v1/", "OpenAi")]
    [InlineData("https://api.openai.com/v1", "OpenAi")]
    [InlineData("https://openrouter.ai/api/v1/", "OpenRouter")]
    [InlineData("https://OPENROUTER.AI/api/v1/", "OpenRouter")]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/openai/", "GeminiShim")]
    [InlineData("http://localhost:11434/v1/", "Other")]
    [InlineData("https://api.groq.com/openai/v1/", "Other")]
    [InlineData("", "Other")]
    [InlineData(null, "Other")]
    public void Resolve_MapsBaseUrlToDialect(string? baseUrl, string expectedName)
    {
        Assert.Equal(expectedName, OpenAiDialectResolver.Resolve(baseUrl).ToString());
    }
}
