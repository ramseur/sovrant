using Sovrant.Runtime.Metrics;

namespace Sovrant.Runtime.Tests.Metrics;

public sealed class OpenRouterCostModelTests
{
    [Fact]
    public void ComputeCost_BasicPromptCompletion_ReturnsCorrectSum()
    {
        var pricing = new ModelPricing
        {
            Prompt = "0.000003",       // $3/M input tokens
            Completion = "0.000015",   // $15/M output tokens
            Request = "0"
        };

        var cost = OpenRouterCostModel.ComputeCost(pricing, 1000, 500, hints: null);

        // 1000 * 0.000003 = 0.003
        // 500 * 0.000015 = 0.0075
        // Total = 0.0105
        Assert.NotNull(cost);
        Assert.Equal(0.0105m, cost.Value);
    }

    [Fact]
    public void ComputeCost_WithRequestFee_IncludesFlatFee()
    {
        var pricing = new ModelPricing
        {
            Prompt = "0.000003",
            Completion = "0.000015",
            Request = "0.01"
        };

        var cost = OpenRouterCostModel.ComputeCost(pricing, 1000, 500, hints: null);

        Assert.NotNull(cost);
        Assert.Equal(0.0105m + 0.01m, cost.Value);
    }

    [Fact]
    public void ComputeCost_WithCacheHints_IncludesCacheCosts()
    {
        var pricing = new ModelPricing
        {
            Prompt = "0.000003",
            Completion = "0.000015",
            Request = "0",
            InputCacheRead = "0.0000003",
            InputCacheWrite = "0.00000375"
        };

        var hints = new CostHints
        {
            CacheReadTokens = 5000,
            CacheWriteTokens = 2000
        };

        var cost = OpenRouterCostModel.ComputeCost(pricing, 1000, 500, hints);

        Assert.NotNull(cost);
        var baseCost = (1000m * 0.000003m) + (500m * 0.000015m);
        var cacheReadCost = 5000m * 0.0000003m;
        var cacheWriteCost = 2000m * 0.00000375m;
        Assert.Equal(baseCost + cacheReadCost + cacheWriteCost, cost.Value);
    }

    [Fact]
    public void ComputeCost_WithImageHints_IncludesImageCost()
    {
        var pricing = new ModelPricing
        {
            Prompt = "0.000003",
            Completion = "0.000015",
            Request = "0",
            Image = "0.0048"
        };

        var hints = new CostHints { ImageCount = 3 };

        var cost = OpenRouterCostModel.ComputeCost(pricing, 1000, 500, hints);

        Assert.NotNull(cost);
        var baseCost = (1000m * 0.000003m) + (500m * 0.000015m);
        Assert.Equal(baseCost + (3m * 0.0048m), cost.Value);
    }

    [Fact]
    public void ComputeCost_NullPricing_ReturnsNull()
    {
        var pricing = new ModelPricing
        {
            Prompt = null,
            Completion = "0.000015"
        };

        var cost = OpenRouterCostModel.ComputeCost(pricing, 1000, 500, hints: null);
        Assert.Null(cost);
    }

    [Fact]
    public void ComputeCost_ZeroTokens_ReturnsZero()
    {
        var pricing = new ModelPricing
        {
            Prompt = "0.000003",
            Completion = "0.000015",
            Request = "0"
        };

        var cost = OpenRouterCostModel.ComputeCost(pricing, 0, 0, hints: null);
        Assert.NotNull(cost);
        Assert.Equal(0m, cost.Value);
    }
}
