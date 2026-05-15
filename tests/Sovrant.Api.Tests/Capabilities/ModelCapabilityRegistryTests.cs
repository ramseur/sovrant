using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Capabilities;

namespace Sovrant.Api.Tests.Capabilities;

/// <summary>Tests for <see cref="ModelCapabilityRegistry"/>.</summary>
public sealed class ModelCapabilityRegistryTests
{
    private readonly ModelCapabilityRegistry _registry = new(NullLogger<ModelCapabilityRegistry>.Instance);

    // ── Exact match ────────────────────────────────────────────────────

    [Fact]
    public void GetCapabilities_ReturnsDefaults_ForUnknownModel()
    {
        var caps = _registry.GetCapabilities("unknown/model");

        Assert.True(caps.NativeTools);
        Assert.True(caps.StructuredOutput);
        Assert.Equal(CapabilitySource.Default, caps.Source);
    }

    [Fact]
    public void GetCapabilities_ReturnsExact_WhenRegistered()
    {
        var caps = new ModelCapabilities { NativeTools = false, Source = CapabilitySource.Bundled };
        _registry.Register("google/gemma-4-27b", caps);

        var result = _registry.GetCapabilities("google/gemma-4-27b");

        Assert.False(result.NativeTools);
        Assert.Equal(CapabilitySource.Bundled, result.Source);
    }

    [Fact]
    public void GetCapabilities_IsCaseInsensitive()
    {
        _registry.Register("Google/Gemma-4-27B", new ModelCapabilities { NativeTools = false, Source = CapabilitySource.Bundled });

        var result = _registry.GetCapabilities("google/gemma-4-27b");

        Assert.False(result.NativeTools);
    }

    // ── Priority ───────────────────────────────────────────────────────

    [Fact]
    public void Register_HigherSourceOverwritesLower()
    {
        _registry.Register("model-x", new ModelCapabilities { NativeTools = false, Source = CapabilitySource.Live });
        _registry.Register("model-x", new ModelCapabilities { NativeTools = true, Source = CapabilitySource.Bundled });

        var result = _registry.GetCapabilities("model-x");

        Assert.True(result.NativeTools);
        Assert.Equal(CapabilitySource.Bundled, result.Source);
    }

    [Fact]
    public void Register_LowerSourceDoesNotOverwriteHigher()
    {
        _registry.Register("model-x", new ModelCapabilities { NativeTools = true, Source = CapabilitySource.User });
        _registry.Register("model-x", new ModelCapabilities { NativeTools = false, Source = CapabilitySource.Live });

        var result = _registry.GetCapabilities("model-x");

        Assert.True(result.NativeTools);
        Assert.Equal(CapabilitySource.User, result.Source);
    }

    [Fact]
    public void Register_SameSourceOverwrites()
    {
        _registry.Register("model-x", new ModelCapabilities { NativeTools = false, Source = CapabilitySource.Bundled });
        _registry.Register("model-x", new ModelCapabilities { NativeTools = true, Source = CapabilitySource.Bundled });

        var result = _registry.GetCapabilities("model-x");

        Assert.True(result.NativeTools);
    }

    // ── Glob matching ──────────────────────────────────────────────────

    [Fact]
    public void GetCapabilities_MatchesGlobPattern()
    {
        _registry.Register("google/gemma-4-*", new ModelCapabilities
        {
            OllamaTemplateWorkaround = false,
            Source = CapabilitySource.Bundled,
        });

        var result = _registry.GetCapabilities("google/gemma-4-27b");

        Assert.False(result.OllamaTemplateWorkaround);
        Assert.Equal(CapabilitySource.Bundled, result.Source);
    }

    [Fact]
    public void GetCapabilities_ExactMatchTakesPriorityOverGlob()
    {
        _registry.Register("google/gemma-4-*", new ModelCapabilities
        {
            NativeTools = false,
            Source = CapabilitySource.Bundled,
        });
        _registry.Register("google/gemma-4-27b", new ModelCapabilities
        {
            NativeTools = true,
            Source = CapabilitySource.Live,
        });

        var result = _registry.GetCapabilities("google/gemma-4-27b");

        // Exact match wins even though glob has higher source
        Assert.True(result.NativeTools);
    }

    [Fact]
    public void GetCapabilities_HigherSourceGlobWinsOverLower()
    {
        _registry.Register("google/gemma-4-*", new ModelCapabilities
        {
            NativeTools = false,
            Source = CapabilitySource.Live,
        });
        _registry.Register("google/gemma-4-*", new ModelCapabilities
        {
            NativeTools = true,
            Source = CapabilitySource.User,
        });

        var result = _registry.GetCapabilities("google/gemma-4-9b");

        Assert.True(result.NativeTools);
        Assert.Equal(CapabilitySource.User, result.Source);
    }

    [Fact]
    public void GetCapabilities_GlobDoesNotMatchPartial()
    {
        _registry.Register("google/gemma-4-*", new ModelCapabilities
        {
            NativeTools = false,
            Source = CapabilitySource.Bundled,
        });

        // Different prefix should not match
        var result = _registry.GetCapabilities("meta/llama-3-70b");

        Assert.True(result.NativeTools); // default
        Assert.Equal(CapabilitySource.Default, result.Source);
    }

    // ── Aliases / Normalize ────────────────────────────────────────────

    [Fact]
    public void Normalize_ReturnsCanonical_WhenAliasRegistered()
    {
        _registry.RegisterAlias("gemma4:27b", "google/gemma-4-27b");

        Assert.Equal("google/gemma-4-27b", _registry.Normalize("gemma4:27b"));
    }

    [Fact]
    public void Normalize_ReturnsInputUnchanged_WhenNoAlias()
    {
        Assert.Equal("some-model", _registry.Normalize("some-model"));
    }

    [Fact]
    public void GetCapabilities_ResolvesAlias_BeforeLookup()
    {
        _registry.Register("google/gemma-4-27b", new ModelCapabilities
        {
            NativeTools = false,
            Source = CapabilitySource.Bundled,
        });
        _registry.RegisterAlias("gemma4:27b", "google/gemma-4-27b");

        var result = _registry.GetCapabilities("gemma4:27b");

        Assert.False(result.NativeTools);
        Assert.Equal(CapabilitySource.Bundled, result.Source);
    }

    // ── GetAll ──────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsRegisteredModels()
    {
        _registry.Register("model-a", new ModelCapabilities { Source = CapabilitySource.Bundled });
        _registry.Register("model-b", new ModelCapabilities { Source = CapabilitySource.Live });

        var all = _registry.GetAll();

        Assert.Equal(2, all.Count);
        Assert.True(all.ContainsKey("model-a"));
        Assert.True(all.ContainsKey("model-b"));
    }

    [Fact]
    public void GetAll_DoesNotIncludeGlobPatterns()
    {
        _registry.Register("google/gemma-4-*", new ModelCapabilities { Source = CapabilitySource.Bundled });
        _registry.Register("model-a", new ModelCapabilities { Source = CapabilitySource.Bundled });

        var all = _registry.GetAll();

        Assert.Single(all);
        Assert.True(all.ContainsKey("model-a"));
    }
}
