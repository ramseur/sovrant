using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class PlanPresenterTests
{
    private readonly PlanPresenter _presenter = new();

    [Fact]
    public void FormatPlan_IncludesGoalAndSteps()
    {
        var plan = new RuntimePlan("p1", 1, "Fix auth bug",
        [
            new RuntimeStep(0, "Read auth.cs", "File contents", RuntimeModelTier.Fast,
                AllowedTools: ["Read"]),
            new RuntimeStep(1, "Edit the token check", "Fixed code", RuntimeModelTier.Standard,
                AllowedTools: ["Edit"]),
            new RuntimeStep(2, "Run tests", "Tests pass", RuntimeModelTier.Standard,
                AllowedTools: ["Bash"]),
        ], DateTimeOffset.UtcNow);

        var formatted = _presenter.FormatPlan(plan);

        Assert.Contains("Fix auth bug", formatted);
        Assert.Contains("(1) Read auth.cs", formatted);
        Assert.Contains("(2) Edit the token check", formatted);
        Assert.Contains("(3) Run tests", formatted);
    }

    [Fact]
    public void FormatPlan_MarksDangerousSteps()
    {
        var plan = new RuntimePlan("p1", 1, "Deploy",
        [
            new RuntimeStep(0, "Run deploy script", "Deployed", RuntimeModelTier.Standard,
                AllowedTools: ["Bash"]),
        ], DateTimeOffset.UtcNow);

        var formatted = _presenter.FormatPlan(plan);

        Assert.Contains("[DANGEROUS]", formatted);
    }

    [Fact]
    public void FormatPlan_MarksEscalationSteps()
    {
        var plan = new RuntimePlan("p1", 1, "Complex task",
        [
            new RuntimeStep(0, "Delegate to swarm", "Swarm result", RuntimeModelTier.High,
                AllowedTools: ["Swarm"]),
        ], DateTimeOffset.UtcNow);

        var formatted = _presenter.FormatPlan(plan);

        Assert.Contains("[ESCALATION]", formatted);
    }

    [Fact]
    public void FormatPlan_NoMarkerForSafeSteps()
    {
        var plan = new RuntimePlan("p1", 1, "Read files",
        [
            new RuntimeStep(0, "Read config", "Config contents", RuntimeModelTier.Fast,
                AllowedTools: ["Read", "Grep"]),
        ], DateTimeOffset.UtcNow);

        var formatted = _presenter.FormatPlan(plan);

        Assert.DoesNotContain("[DANGEROUS]", formatted);
        Assert.DoesNotContain("[ESCALATION]", formatted);
    }
}
