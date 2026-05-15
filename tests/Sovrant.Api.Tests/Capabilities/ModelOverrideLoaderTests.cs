using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Capabilities;

namespace Sovrant.Api.Tests.Capabilities;

/// <summary>Tests for <see cref="ModelOverrideLoader"/>.</summary>
public sealed class ModelOverrideLoaderTests
{
    private readonly ModelCapabilityRegistry _registry = new(NullLogger<ModelCapabilityRegistry>.Instance);

    [Fact]
    public void LoadAll_RegistersBundledOverrides()
    {
        var loader = new ModelOverrideLoader(_registry, NullLogger<ModelOverrideLoader>.Instance);

        loader.LoadAll();

        // The bundled model-overrides.json has a google/gemma-4-* glob
        var caps = _registry.GetCapabilities("google/gemma-4-27b");
        Assert.Equal(CapabilitySource.Bundled, caps.Source);
        Assert.True(caps.NativeTools);
        Assert.True(caps.ThinkingMode);
        Assert.False(caps.OllamaTemplateWorkaround);
        Assert.Equal("gemma-4", caps.Family);
        Assert.Equal(262144, caps.MaxContext);
    }

    [Fact]
    public void LoadAll_RegistersAliases()
    {
        var loader = new ModelOverrideLoader(_registry, NullLogger<ModelOverrideLoader>.Instance);

        loader.LoadAll();

        Assert.Equal("google/gemma-4-27b", _registry.Normalize("gemma4:27b"));
        Assert.Equal("google/gemma-4-27b", _registry.Normalize("gemma4:latest"));
    }

    [Fact]
    public void LoadAll_OllamaGlob_SetsWorkaround()
    {
        var loader = new ModelOverrideLoader(_registry, NullLogger<ModelOverrideLoader>.Instance);

        loader.LoadAll();

        // gemma4:* should have ollama_template_workaround = true
        var caps = _registry.GetCapabilities("gemma4:12b");
        Assert.True(caps.OllamaTemplateWorkaround);
        Assert.Equal(CapabilitySource.Bundled, caps.Source);
    }

    [Theory]
    [InlineData("gpt-4o-mini", "gpt-4o")]
    [InlineData("gpt-4o-2024-08-06", "gpt-4o")]
    [InlineData("gpt-4-turbo-preview", "gpt-4")]
    [InlineData("gpt-4.1", "gpt-4.1")]
    [InlineData("o1-preview", "o1")]
    [InlineData("o3-mini", "o3")]
    [InlineData("o4-mini", "o4")]
    [InlineData("claude-3-5-sonnet-20241022", "claude-3.5")]
    [InlineData("claude-3-7-sonnet-latest", "claude-3.7")]
    [InlineData("claude-sonnet-4-5", "claude-4")]
    [InlineData("claude-opus-4-7", "claude-4")]
    [InlineData("claude-haiku-4-5-20251001", "claude-4")]
    [InlineData("gemini-1.5-pro", "gemini-1.5")]
    [InlineData("gemini-2.0-flash", "gemini-2")]
    [InlineData("openai/gpt-4o-mini:online", null)]
    public void LoadAll_NativeWebSearchSupportedModels_FlagSet(string modelId, string? expectedFamily)
    {
        var loader = new ModelOverrideLoader(_registry, NullLogger<ModelOverrideLoader>.Instance);

        loader.LoadAll();

        var caps = _registry.GetCapabilities(modelId);
        Assert.True(caps.SupportsNativeWebSearch, $"Expected {modelId} to support native web search");
        Assert.Equal(CapabilitySource.Bundled, caps.Source);
        if (expectedFamily is not null)
            Assert.Equal(expectedFamily, caps.Family);
    }

    [Fact]
    public void LoadAll_UnsupportedModel_NativeWebSearchFalse()
    {
        var loader = new ModelOverrideLoader(_registry, NullLogger<ModelOverrideLoader>.Instance);

        loader.LoadAll();

        // Llama models have no native web search wiring
        var caps = _registry.GetCapabilities("meta-llama/llama-3-70b-instruct");
        Assert.False(caps.SupportsNativeWebSearch);
    }

    [Fact]
    public void LoadAll_EnvironmentVariableNativeWebSearch_OverridesDefault()
    {
        var envKey = "SOVRANT_MODEL_CAPABILITIES";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "custom-model:supports_native_web_search=true");

            var loader = new ModelOverrideLoader(_registry, NullLogger<ModelOverrideLoader>.Instance);
            loader.LoadAll();

            var caps = _registry.GetCapabilities("custom-model");
            Assert.True(caps.SupportsNativeWebSearch);
            Assert.Equal(CapabilitySource.User, caps.Source);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }

    [Fact]
    public void LoadAll_EnvironmentVariable_OverridesBundled()
    {
        // Set env var before loading
        var envKey = "SOVRANT_MODEL_CAPABILITIES";
        var original = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "google/gemma-4-27b:native_tools=false");

            var loader = new ModelOverrideLoader(_registry, NullLogger<ModelOverrideLoader>.Instance);
            loader.LoadAll();

            var caps = _registry.GetCapabilities("google/gemma-4-27b");
            // Env override (User source) should override the bundled glob
            Assert.False(caps.NativeTools);
            Assert.Equal(CapabilitySource.User, caps.Source);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, original);
        }
    }
}
