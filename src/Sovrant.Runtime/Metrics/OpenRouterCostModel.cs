using System.Globalization;

namespace Sovrant.Runtime.Metrics;

/// <summary>
/// <see cref="ICostModel"/> implementation backed by the OpenRouter pricing catalogue.
/// Normalises Sovrant model ids, looks up pricing from the cached snapshot,
/// and computes USD cost as <c>prompt * inputTokens + completion * outputTokens + extras</c>.
/// </summary>
public sealed class OpenRouterCostModel : ICostModel
{
    private readonly OpenRouterPricingClient _pricingClient;
    private readonly ModelIdNormaliser _normaliser;

    public OpenRouterCostModel(OpenRouterPricingClient pricingClient, ModelIdNormaliser normaliser)
    {
        _pricingClient = pricingClient;
        _normaliser = normaliser;
    }

    public decimal? EstimateCost(string model, long inputTokens, long outputTokens, CostHints? hints = null)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;

        var openRouterId = _normaliser.Normalise(model);
        if (openRouterId is null)
            return null;

        // GetSnapshotAsync is async but we need a sync path here.
        // The snapshot is already in memory after the first call — this blocks only on the first invocation.
        var snapshot = _pricingClient.GetSnapshotAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        if (snapshot is null)
            return null;

        if (!snapshot.Models.TryGetValue(openRouterId, out var pricing))
            return null;

        return ComputeCost(pricing, inputTokens, outputTokens, hints);
    }

    internal static decimal? ComputeCost(ModelPricing pricing, long inputTokens, long outputTokens, CostHints? hints)
    {
        if (!TryParseDecimal(pricing.Prompt, out var promptRate) ||
            !TryParseDecimal(pricing.Completion, out var completionRate))
            return null;

        var cost = (promptRate * inputTokens) + (completionRate * outputTokens);

        // Per-request flat fee
        if (TryParseDecimal(pricing.Request, out var requestRate) && requestRate > 0)
            cost += requestRate;

        if (hints is not null)
        {
            // Cache read tokens (cheaper than prompt)
            if (hints.CacheReadTokens > 0 && TryParseDecimal(pricing.InputCacheRead, out var cacheReadRate))
                cost += cacheReadRate * hints.CacheReadTokens;

            // Cache write tokens
            if (hints.CacheWriteTokens > 0 && TryParseDecimal(pricing.InputCacheWrite, out var cacheWriteRate))
                cost += cacheWriteRate * hints.CacheWriteTokens;

            // Images
            if (hints.ImageCount > 0 && TryParseDecimal(pricing.Image, out var imageRate))
                cost += imageRate * hints.ImageCount;

            // Internal reasoning tokens
            if (hints.InternalReasoningTokens > 0 && TryParseDecimal(pricing.InternalReasoning, out var reasoningRate))
                cost += reasoningRate * hints.InternalReasoningTokens;

            // Web search
            if (hints.IncludesWebSearch && TryParseDecimal(pricing.WebSearch, out var webSearchRate) && webSearchRate > 0)
                cost += webSearchRate;
        }

        return cost;
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0;
        return value is not null &&
               decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }
}
