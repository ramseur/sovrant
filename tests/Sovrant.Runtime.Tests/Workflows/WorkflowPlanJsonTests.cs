using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Runtime.Tests.Workflows;

public sealed class WorkflowPlanJsonTests
{
    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsStepsInOrder()
    {
        var plan = new RuntimePlan("plan-1", 2, "the goal",
        [
            new RuntimeStep(0, "first", "first done", RuntimeModelTier.High),
            new RuntimeStep(1, "second", "second done", RuntimeModelTier.Fast),
        ], DateTimeOffset.UtcNow);

        var json = WorkflowPlanJson.Serialize(plan);
        var roundTripped = WorkflowPlanJson.TryDeserialize(json, fallbackGoal: "unused");

        Assert.NotNull(roundTripped);
        Assert.Equal(2, roundTripped!.Steps.Count);
        Assert.Equal("first", roundTripped.Steps[0].Intent);
        Assert.Equal(RuntimeModelTier.High, roundTripped.Steps[0].ModelTier);
        Assert.Equal("second", roundTripped.Steps[1].Intent);
        Assert.Equal(RuntimeModelTier.Fast, roundTripped.Steps[1].ModelTier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("not json at all")]
    [InlineData("{\"steps\":[]}")]
    public void TryDeserialize_EmptyOrInvalidOrNoSteps_ReturnsNull(string input)
    {
        Assert.Null(WorkflowPlanJson.TryDeserialize(input, fallbackGoal: "g"));
    }

    [Fact]
    public void TryDeserialize_UnknownTier_DefaultsToStandard()
    {
        var json = "{\"steps\":[{\"index\":0,\"intent\":\"x\",\"expected\":\"y\",\"tier\":\"weird\"}]}";
        var plan = WorkflowPlanJson.TryDeserialize(json, fallbackGoal: "g");

        Assert.NotNull(plan);
        Assert.Equal(RuntimeModelTier.Standard, plan!.Steps[0].ModelTier);
    }
}
