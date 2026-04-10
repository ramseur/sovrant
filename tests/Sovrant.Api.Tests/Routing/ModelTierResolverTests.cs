using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Capabilities;
using Sovrant.Api.Routing;

namespace Sovrant.Api.Tests.Routing;

/// <summary>Tests for <see cref="ModelTierResolver"/>.</summary>
public sealed class ModelTierResolverTests
{
    private readonly ModelCapabilityRegistry _registry = new(NullLogger<ModelCapabilityRegistry>.Instance);

    private ModelTierResolver CreateResolver(IReadOnlyDictionary<string, string>? pinnedTiers = null, bool freeModelsOnly = false) =>
        new(_registry, NullLogger<ModelTierResolver>.Instance, pinnedTiers, freeModelsOnly);

    // ── Explicit TierHint ───────────────────────────────────────────────

    [Fact]
    public void Resolve_UsesExplicitTierHint()
    {
        _registry.Register("cheap-model", new ModelCapabilities
        {
            TierHint = ModelTier.Fast,
            CostPerMillionInput = 0.1m,
            Source = CapabilitySource.Live,
        });
        _registry.Register("expensive-model", new ModelCapabilities
        {
            TierHint = ModelTier.High,
            CostPerMillionInput = 30m,
            Source = CapabilitySource.Live,
        });

        var resolver = CreateResolver();
        Assert.Equal("cheap-model", resolver.Resolve(ModelTier.Fast));
        Assert.Equal("expensive-model", resolver.Resolve(ModelTier.High));
    }

    // ── Auto-tier from pricing ──────────────────────────────────────────

    [Fact]
    public void Resolve_AutoAssignsByPricing_WhenNoTierHint()
    {
        _registry.Register("model-cheap", new ModelCapabilities
        {
            CostPerMillionInput = 0.1m,
            Source = CapabilitySource.Live,
        });
        _registry.Register("model-mid", new ModelCapabilities
        {
            CostPerMillionInput = 5m,
            Source = CapabilitySource.Live,
        });
        _registry.Register("model-expensive", new ModelCapabilities
        {
            CostPerMillionInput = 30m,
            Source = CapabilitySource.Live,
        });

        var resolver = CreateResolver();

        // With 3 models: index 0 → fast, index 1 → standard, index 2 → high
        Assert.Equal("model-cheap", resolver.Resolve(ModelTier.Fast));
        Assert.Equal("model-expensive", resolver.Resolve(ModelTier.High));
    }

    // ── Tier collapse ───────────────────────────────────────────────────

    [Fact]
    public void Resolve_CollapsesToNextTier_WhenEmpty()
    {
        _registry.Register("only-model", new ModelCapabilities
        {
            TierHint = ModelTier.Standard,
            CostPerMillionInput = 5m,
            Source = CapabilitySource.Live,
        });

        var resolver = CreateResolver();

        // Fast tier is empty → should collapse to standard
        var result = resolver.Resolve(ModelTier.Fast);
        Assert.Equal("only-model", result);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoModelsAtAll()
    {
        var resolver = CreateResolver();
        Assert.Null(resolver.Resolve(ModelTier.Standard));
    }

    // ── Pinned tiers ────────────────────────────────────────────────────

    [Fact]
    public void Resolve_UsesPinnedModel_WhenConfigured()
    {
        _registry.Register("auto-model", new ModelCapabilities
        {
            TierHint = ModelTier.Fast,
            CostPerMillionInput = 0.1m,
            Source = CapabilitySource.Live,
        });

        var pinned = new Dictionary<string, string> { [ModelTier.Fast] = "my-pinned-model" };
        var resolver = CreateResolver(pinned);

        Assert.Equal("my-pinned-model", resolver.Resolve(ModelTier.Fast));
    }

    [Fact]
    public void Resolve_IgnoresPinnedAuto()
    {
        _registry.Register("auto-model", new ModelCapabilities
        {
            TierHint = ModelTier.Fast,
            CostPerMillionInput = 0.1m,
            Source = CapabilitySource.Live,
        });

        var pinned = new Dictionary<string, string> { [ModelTier.Fast] = "auto" };
        var resolver = CreateResolver(pinned);

        // "auto" means use auto-assignment, not literal "auto"
        Assert.Equal("auto-model", resolver.Resolve(ModelTier.Fast));
    }

    // ── Intent affinity ─────────────────────────────────────────────────

    [Fact]
    public void Resolve_PrefersModelWithIntentAffinity()
    {
        _registry.Register("general-model", new ModelCapabilities
        {
            TierHint = ModelTier.Standard,
            CostPerMillionInput = 5m,
            Source = CapabilitySource.Live,
        });
        _registry.Register("code-model", new ModelCapabilities
        {
            TierHint = ModelTier.Standard,
            CostPerMillionInput = 6m, // slightly more expensive
            IntentAffinity = new Dictionary<string, float>
            {
                [nameof(IntentClass.CodeGeneration)] = 10f, // big boost
            },
            Source = CapabilitySource.Live,
        });

        var resolver = CreateResolver();

        // Without intent: cheaper model wins
        Assert.Equal("general-model", resolver.Resolve(ModelTier.Standard));

        // With code gen intent: code-model wins despite higher price
        Assert.Equal("code-model", resolver.Resolve(ModelTier.Standard, IntentClass.CodeGeneration));
    }

    // ── Name-based inference for local models ───────────────────────────

    [Fact]
    public void Resolve_InfersTierFromModelName()
    {
        _registry.Register("qwen2.5:3b", new ModelCapabilities
        {
            Source = CapabilitySource.Live,
        });
        _registry.Register("qwen2.5:72b", new ModelCapabilities
        {
            Source = CapabilitySource.Live,
        });

        var resolver = CreateResolver();

        // 3b should go to fast, 72b to high
        var assignments = resolver.GetTierAssignments();
        Assert.Contains(assignments[ModelTier.Fast], c => c.ModelId == "qwen2.5:3b");
        Assert.Contains(assignments[ModelTier.High], c => c.ModelId == "qwen2.5:72b");
    }

    // ── GetTierAssignments ──────────────────────────────────────────────

    [Fact]
    public void GetTierAssignments_ReturnsAllTiers()
    {
        var resolver = CreateResolver();
        var assignments = resolver.GetTierAssignments();

        Assert.True(assignments.ContainsKey(ModelTier.Fast));
        Assert.True(assignments.ContainsKey(ModelTier.Standard));
        Assert.True(assignments.ContainsKey(ModelTier.High));
    }

    // ── Rebuild ─────────────────────────────────────────────────────────

    [Fact]
    public void Rebuild_PicksUpNewModels()
    {
        var resolver = CreateResolver();
        Assert.Null(resolver.Resolve(ModelTier.Fast));

        _registry.Register("new-fast-model", new ModelCapabilities
        {
            TierHint = ModelTier.Fast,
            CostPerMillionInput = 0.1m,
            Source = CapabilitySource.Live,
        });

        resolver.Rebuild();
        Assert.Equal("new-fast-model", resolver.Resolve(ModelTier.Fast));
    }

    // ── Free models only ───────────────────────────────────────────────

    [Fact]
    public void Resolve_FreeModelsOnly_ExcludesPaidModels()
    {
        _registry.Register("paid-model", new ModelCapabilities
        {
            TierHint = ModelTier.Fast,
            CostPerMillionInput = 0.5m,
            Source = CapabilitySource.Live,
        });
        _registry.Register("free-model:free", new ModelCapabilities
        {
            TierHint = ModelTier.Fast,
            Source = CapabilitySource.Live,
            // No pricing — treated as free
        });

        var resolver = CreateResolver(freeModelsOnly: true);
        Assert.Equal("free-model:free", resolver.Resolve(ModelTier.Fast));
    }

    [Fact]
    public void Resolve_FreeModelsOnly_IncludesLocalModelsWithoutPricing()
    {
        _registry.Register("ollama/llama3:8b", new ModelCapabilities
        {
            Source = CapabilitySource.Live,
            // No pricing data → local/self-hosted → considered free
        });
        _registry.Register("paid-cloud", new ModelCapabilities
        {
            TierHint = ModelTier.Standard,
            CostPerMillionInput = 10m,
            Source = CapabilitySource.Live,
        });

        var resolver = CreateResolver(freeModelsOnly: true);
        var assignments = resolver.GetTierAssignments();

        // Local model should be included
        var allModels = assignments.Values.SelectMany(c => c).Select(c => c.ModelId).ToList();
        Assert.Contains("ollama/llama3:8b", allModels);
        Assert.DoesNotContain("paid-cloud", allModels);
    }

    [Fact]
    public void Resolve_FreeModelsOnly_IncludesOpenRouterFreeSuffix()
    {
        _registry.Register("google/gemma-4-31b-it", new ModelCapabilities
        {
            CostPerMillionInput = 0.3m,
            CostPerMillionOutput = 0.3m,
            Source = CapabilitySource.Live,
        });
        _registry.Register("google/gemma-4-31b-it:free", new ModelCapabilities
        {
            // Free variants on OpenRouter have no cost
            Source = CapabilitySource.Live,
        });

        var resolver = CreateResolver(freeModelsOnly: true);
        var allModels = resolver.GetTierAssignments()
            .Values.SelectMany(c => c).Select(c => c.ModelId).ToList();

        Assert.Contains("google/gemma-4-31b-it:free", allModels);
        Assert.DoesNotContain("google/gemma-4-31b-it", allModels);
    }
}
