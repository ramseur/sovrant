using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Capabilities;

namespace Sovrant.Api.Routing;

/// <summary>
/// Resolves model tiers to concrete model IDs by auto-assigning models
/// from the <see cref="IModelCapabilityRegistry"/> into <c>fast</c>,
/// <c>standard</c>, and <c>high</c> buckets based on pricing data,
/// explicit <see cref="ModelCapabilities.TierHint"/> values, or parameter
/// count heuristics for local models.
/// </summary>
public sealed partial class ModelTierResolver : IModelTierResolver
{
    private readonly IModelCapabilityRegistry _registry;
    private readonly ILogger<ModelTierResolver> _logger;
    private readonly Dictionary<string, string>? _pinnedTierModels;
    private readonly bool _freeModelsOnly;

    // tier → candidates sorted by score ascending (cheapest/best first)
    private Dictionary<string, List<TierCandidate>> _assignments = new(StringComparer.OrdinalIgnoreCase);

    public ModelTierResolver(
        IModelCapabilityRegistry registry,
        ILogger<ModelTierResolver> logger,
        IReadOnlyDictionary<string, string>? pinnedTierModels = null,
        bool freeModelsOnly = false)
    {
        _registry = registry;
        _logger = logger;
        _freeModelsOnly = freeModelsOnly;
        _pinnedTierModels = pinnedTierModels?.ToDictionary(
            kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        Rebuild();
    }

    /// <inheritdoc/>
    public string? Resolve(string tier, IntentClass? intent = null, bool requireTools = false)
    {
        // Check for pinned model first
        if (_pinnedTierModels is not null
            && _pinnedTierModels.TryGetValue(tier, out var pinned)
            && !string.Equals(pinned, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return pinned;
        }

        if (!_assignments.TryGetValue(tier, out var candidates) || candidates.Count == 0)
        {
            // Collapse to next-best tier
            return CollapseTier(tier, intent, requireTools);
        }

        // Filter to tool-capable models when the request includes tools
        var eligible = requireTools
            ? candidates.Where(c => _registry.GetCapabilities(c.ModelId).NativeTools).ToList()
            : candidates;

        if (eligible.Count == 0)
            return CollapseTier(tier, intent, requireTools);

        if (intent is not null && eligible.Count > 1)
        {
            // Check for intent affinity boosts
            var intentName = intent.Value.ToString();
            var best = eligible[0]; // default: cheapest
            var bestScore = float.MaxValue;

            foreach (var c in eligible)
            {
                var caps = _registry.GetCapabilities(c.ModelId);
                var affinityBoost = 0f;
                if (caps.IntentAffinity is not null
                    && caps.IntentAffinity.TryGetValue(intentName, out var boost))
                {
                    affinityBoost = boost;
                }

                // Lower is better; affinity lowers the effective score
                var effectiveScore = (float)c.Score - affinityBoost;
                if (effectiveScore < bestScore)
                {
                    bestScore = effectiveScore;
                    best = c;
                }
            }
            return best.ModelId;
        }

        return eligible[0].ModelId;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IReadOnlyList<TierCandidate>> GetTierAssignments()
    {
        var result = new Dictionary<string, IReadOnlyList<TierCandidate>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tier, candidates) in _assignments)
            result[tier] = candidates.AsReadOnly();
        return result;
    }

    /// <inheritdoc/>
    public void Rebuild()
    {
        var all = _registry.GetAll();
        var scored = new List<(string ModelId, ModelCapabilities Caps, decimal Score)>();

        foreach (var (modelId, caps) in all)
        {
            // Skip glob patterns
            if (modelId.Contains('*', StringComparison.Ordinal))
                continue;

            // Free-models-only: include only models with zero cost or ":free" suffix
            if (_freeModelsOnly && !IsFreeModel(modelId, caps))
                continue;

            var score = ComputeScore(modelId, caps);
            scored.Add((modelId, caps, score));
        }

        // Sort by score ascending (cheapest first)
        scored.Sort((a, b) => a.Score.CompareTo(b.Score));

        var newAssignments = new Dictionary<string, List<TierCandidate>>(StringComparer.OrdinalIgnoreCase)
        {
            [ModelTier.Fast] = [],
            [ModelTier.Standard] = [],
            [ModelTier.High] = [],
        };

        // First pass: assign models with explicit TierHint
        foreach (var (modelId, caps, score) in scored)
        {
            if (!string.IsNullOrEmpty(caps.TierHint)
                && newAssignments.TryGetValue(caps.TierHint, out var bucket))
            {
                bucket.Add(new TierCandidate(modelId, score, caps.TierHint));
            }
        }

        // Second pass: auto-assign models without TierHint using pricing percentiles
        var unassigned = scored.Where(s => string.IsNullOrEmpty(s.Caps.TierHint)).ToList();
        if (unassigned.Count > 0)
        {
            AutoAssignByPricing(unassigned, newAssignments);
        }

        _assignments = newAssignments;

        foreach (var tier in ModelTier.All)
        {
            if (newAssignments.TryGetValue(tier, out var candidates) && candidates.Count > 0)
                LogTierAssignment(tier, candidates.Count, candidates[0].ModelId);
        }
    }

    /// <summary>
    /// Auto-assigns models to tiers based on pricing percentiles.
    /// Cheapest 33% → fast, middle 34% → standard, top 33% → high.
    /// Models without pricing go to standard.
    /// </summary>
    private static void AutoAssignByPricing(
        List<(string ModelId, ModelCapabilities Caps, decimal Score)> models,
        Dictionary<string, List<TierCandidate>> assignments)
    {
        // Separate priced vs unpriced
        var priced = models.Where(m => m.Caps.CostPerMillionInput.HasValue).ToList();
        var unpriced = models.Where(m => !m.Caps.CostPerMillionInput.HasValue).ToList();

        if (priced.Count > 0)
        {
            // Sort by input cost
            priced.Sort((a, b) =>
                (a.Caps.CostPerMillionInput ?? 0).CompareTo(b.Caps.CostPerMillionInput ?? 0));

            var fastCut = priced.Count / 3;
            var highCut = priced.Count - priced.Count / 3;

            for (var i = 0; i < priced.Count; i++)
            {
                var (modelId, _, score) = priced[i];
                string tier;
                if (i < fastCut)
                    tier = ModelTier.Fast;
                else if (i >= highCut)
                    tier = ModelTier.High;
                else
                    tier = ModelTier.Standard;

                assignments[tier].Add(new TierCandidate(modelId, score, tier));
            }
        }

        // Unpriced models (e.g. local Ollama) → try to infer from model name
        foreach (var (modelId, caps, score) in unpriced)
        {
            var tier = InferTierFromName(modelId, caps);
            assignments[tier].Add(new TierCandidate(modelId, score, tier));
        }
    }

    /// <summary>
    /// Infers a tier from the model name for local models without pricing.
    /// Uses parameter count heuristics (e.g. "70b" → high, "7b" → fast).
    /// </summary>
    private static string InferTierFromName(string modelId, ModelCapabilities caps)
    {
        // Check context window as a signal
        if (caps.MaxContext is > 100000)
            return ModelTier.High;

        // Try to extract parameter count from name (e.g. "qwen2.5:72b", "llama-3-70b")
        var match = ParamCountPattern().Match(modelId);
        if (match.Success && float.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var paramB))
        {
            if (paramB >= 30f) return ModelTier.High;
            if (paramB >= 10f) return ModelTier.Standard;
            return ModelTier.Fast;
        }

        return ModelTier.Standard; // safe default
    }

    /// <summary>
    /// Returns <c>true</c> if the model is free — either its ID ends with <c>:free</c>
    /// (OpenRouter convention) or it has no pricing data (local / self-hosted).
    /// </summary>
    private static bool IsFreeModel(string modelId, ModelCapabilities caps)
    {
        if (modelId.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
            return true;

        // No pricing info at all → assume local/self-hosted (free)
        if (!caps.CostPerMillionInput.HasValue && !caps.CostPerMillionOutput.HasValue)
            return true;

        // Explicitly zero cost
        return caps.CostPerMillionInput is 0m && caps.CostPerMillionOutput is 0m;
    }

    /// <summary>
    /// Computes a unified score for tier sorting. Lower = cheaper/simpler.
    /// Uses input cost if available, falls back to a context-window proxy.
    /// </summary>
    private static decimal ComputeScore(string modelId, ModelCapabilities caps)
    {
        if (caps.CostPerMillionInput is { } cost and > 0)
            return cost;

        // Fallback: use context window as a rough proxy (larger context ≈ more capable ≈ higher score)
        if (caps.MaxContext is { } ctx and > 0)
            return ctx / 1000m;

        // No data — middle of the pack
        return 50m;
    }

    /// <summary>
    /// When the requested tier has no candidates, collapse to the next-best
    /// available tier rather than failing.
    /// </summary>
    private string? CollapseTier(string tier, IntentClass? intent, bool requireTools = false)
    {
        // fast → standard → high (try cheaper first)
        // high → standard → fast (try more capable first)
        string[] fallbackOrder = tier switch
        {
            ModelTier.Fast => [ModelTier.Standard, ModelTier.High],
            ModelTier.High => [ModelTier.Standard, ModelTier.Fast],
            _ => [ModelTier.High, ModelTier.Fast],
        };

        foreach (var fallback in fallbackOrder)
        {
            if (!_assignments.TryGetValue(fallback, out var candidates) || candidates.Count == 0)
                continue;

            var first = requireTools
                ? candidates.FirstOrDefault(c => _registry.GetCapabilities(c.ModelId).NativeTools)
                : candidates[0];

            if (first is not null)
            {
                LogTierCollapse(tier, fallback);
                return first.ModelId;
            }
        }

        return null; // no models at all
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)[bB]", RegexOptions.Compiled)]
    private static partial Regex ParamCountPattern();

    [LoggerMessage(Level = LogLevel.Information, Message = "Tier '{Tier}': {Count} model(s), default = {DefaultModel}")]
    private partial void LogTierAssignment(string tier, int count, string defaultModel);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tier '{RequestedTier}' empty — collapsed to '{FallbackTier}'")]
    private partial void LogTierCollapse(string requestedTier, string fallbackTier);
}
