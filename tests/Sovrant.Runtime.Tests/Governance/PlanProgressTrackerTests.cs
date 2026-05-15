using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class PlanProgressTrackerTests
{
    [Fact]
    public void CreateEvent_ReturnsCorrectProgress()
    {
        var plan = new RuntimePlan("p1", 1, "Fix bug",
        [
            new RuntimeStep(0, "Read file", "File contents", RuntimeModelTier.Fast),
            new RuntimeStep(1, "Edit file", "Fixed code", RuntimeModelTier.Standard),
            new RuntimeStep(2, "Run tests", "Tests pass", RuntimeModelTier.Standard),
        ], DateTimeOffset.UtcNow);

        var evt = PlanProgressTracker.CreateEvent(plan, plan.Steps[1], "started");

        Assert.Equal(2, evt.Current);       // 1-based
        Assert.Equal(3, evt.Total);
        Assert.Equal("Edit file", evt.Intent);
        Assert.Equal("started", evt.Status);
    }

    [Fact]
    public void StepSummaryEmitter_Summarize()
    {
        var plan = new RuntimePlan("p1", 1, "Fix bug",
        [
            new RuntimeStep(0, "Read config", "Config loaded", RuntimeModelTier.Fast),
            new RuntimeStep(1, "Verify auth", "Auth OK", RuntimeModelTier.Standard),
        ], DateTimeOffset.UtcNow);

        var summary = StepSummaryEmitter.Summarize(plan, plan.Steps[0], "completed");

        Assert.Equal("Step 1/2: Read config [completed]", summary);
    }

    [Fact]
    public void StepSummaryEmitter_FromOutcome()
    {
        var plan = new RuntimePlan("p1", 1, "Fix bug",
        [
            new RuntimeStep(0, "Run tests", "Tests pass", RuntimeModelTier.Standard),
        ], DateTimeOffset.UtcNow);

        var outcome = new StepOutcome(
            StepIndex: 0,
            Status: StepStatus.Failed,
            Summary: "Tests failed",
            ObservationJson: null,
            MatchedExpectation: false,
            StartedAt: DateTimeOffset.UtcNow,
            CompletedAt: DateTimeOffset.UtcNow,
            ErrorMessage: "2 tests failed");

        var summary = StepSummaryEmitter.Summarize(plan, plan.Steps[0], outcome);

        Assert.Equal("Step 1/1: Run tests [failed]", summary);
    }
}
