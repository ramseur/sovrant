namespace Sovrant.Runtime.Metrics;

/// <summary>
/// No-op cost model that always returns <see langword="null"/>.
/// Used when <c>SOVRANT_COST_PROVIDER=none</c> or cost tracking is disabled.
/// </summary>
public sealed class NullCostModel : ICostModel
{
    public static NullCostModel Instance { get; } = new();

    public decimal? EstimateCost(string model, long inputTokens, long outputTokens, CostHints? hints = null)
        => null;
}
