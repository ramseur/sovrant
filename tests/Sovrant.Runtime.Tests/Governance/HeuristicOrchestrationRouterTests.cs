using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class HeuristicOrchestrationRouterTests
{
    private readonly HeuristicOrchestrationRouter _router = new();

    [Fact]
    public void SingleStepPlan_RecommendsDirect()
    {
        var plan = new RuntimePlan("p1", 1, "Read a file",
        [
            new RuntimeStep(0, "Read config.json", "Config contents", RuntimeModelTier.Fast,
                AllowedTools: ["Read"]),
        ], DateTimeOffset.UtcNow);

        var rec = _router.Recommend(plan);

        Assert.Equal(OrchestrationMode.Direct, rec.Mode);
        Assert.True(rec.Confidence > 0.8f);
    }

    [Fact]
    public void ThreeStepPlan_MultipleFiles_RecommendsTeamOrSubAgent()
    {
        var plan = new RuntimePlan("p1", 1, "Update auth",
        [
            new RuntimeStep(0, "Read /src/auth.cs", "Auth code", RuntimeModelTier.Fast,
                AllowedTools: ["Read"]),
            new RuntimeStep(1, "Edit /src/auth.cs", "Fixed code", RuntimeModelTier.Standard,
                AllowedTools: ["Edit"]),
            new RuntimeStep(2, "Edit /src/middleware.cs", "Updated middleware", RuntimeModelTier.Standard,
                AllowedTools: ["Edit"]),
        ], DateTimeOffset.UtcNow);

        var rec = _router.Recommend(plan);

        Assert.True(rec.Mode is OrchestrationMode.Team or OrchestrationMode.SubAgent);
    }

    [Fact]
    public void LargePlan_RecommendsSwarm()
    {
        var steps = Enumerable.Range(0, 10)
            .Select(i => new RuntimeStep(i, $"Edit /src/file{i}.cs", "Updated", RuntimeModelTier.Standard,
                AllowedTools: ["Edit"]))
            .ToArray();

        var plan = new RuntimePlan("p1", 1, "Large refactor", steps, DateTimeOffset.UtcNow);

        var rec = _router.Recommend(plan);

        Assert.Equal(OrchestrationMode.Swarm, rec.Mode);
    }

    [Fact]
    public void EscalationGuard_TrivialTask_NeverSwarmOrMission()
    {
        var plan = new RuntimePlan("p1", 1, "Quick fix",
        [
            new RuntimeStep(0, "Fix typo", "Fixed", RuntimeModelTier.Fast,
                AllowedTools: ["Edit"]),
        ], DateTimeOffset.UtcNow);

        var rec = _router.Recommend(plan);

        Assert.True(rec.Mode is OrchestrationMode.Direct or OrchestrationMode.SubAgent);
    }

    [Fact]
    public void ComplexGoalWithEscalation_RecommendsMission()
    {
        var steps = new[]
        {
            new RuntimeStep(0, "Plan architecture", "Architecture doc", RuntimeModelTier.High),
            new RuntimeStep(1, "Research /src/api.cs", "API analysis", RuntimeModelTier.High),
            new RuntimeStep(2, "Delegate auth to agent", "Auth implemented", RuntimeModelTier.High,
                AllowedTools: ["Agent"]),
            new RuntimeStep(3, "Delegate db to agent", "DB migration", RuntimeModelTier.High,
                AllowedTools: ["Agent"]),
            new RuntimeStep(4, "Delegate tests to agent", "Tests written", RuntimeModelTier.High,
                AllowedTools: ["Agent"]),
            new RuntimeStep(5, "Integration test", "All passing", RuntimeModelTier.High,
                AllowedTools: ["Bash"]),
        };

        var plan = new RuntimePlan("p1", 1, "Rebuild auth system", steps, DateTimeOffset.UtcNow);

        var rec = _router.Recommend(plan);

        Assert.Equal(OrchestrationMode.Mission, rec.Mode);
    }
}
