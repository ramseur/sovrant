using Sovrant.Runtime.Metrics;

namespace Sovrant.Runtime.Tests.Metrics;

public sealed class NullCostModelTests
{
    [Fact]
    public void EstimateCost_AlwaysReturnsNull()
    {
        var model = NullCostModel.Instance;

        Assert.Null(model.EstimateCost("gpt-4o", 1000, 500));
        Assert.Null(model.EstimateCost("claude-sonnet-4-6", 5000, 2000, new CostHints { ImageCount = 3 }));
    }

    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(NullCostModel.Instance, NullCostModel.Instance);
    }
}
