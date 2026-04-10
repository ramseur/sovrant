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
