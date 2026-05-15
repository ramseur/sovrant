using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class PlanApprovalGateTests
{
    private static RuntimePlan MakePlan(params RuntimeStep[] steps) =>
        new("plan-1", 1, "Test goal", steps, DateTimeOffset.UtcNow);

    private static RuntimeStep SafeStep(int index = 0) =>
        new(index, "Read config", "Config contents", RuntimeModelTier.Fast,
            AllowedTools: ["Read", "Grep"]);

    private static RuntimeStep DangerousStep(int index = 1) =>
        new(index, "Run tests", "Tests pass", RuntimeModelTier.Standard,
            AllowedTools: ["Bash"]);

    private static RuntimeStep EscalationStep(int index = 2) =>
        new(index, "Delegate to agent", "Agent result", RuntimeModelTier.High,
            AllowedTools: ["Agent"]);

    [Fact]
    public void AlwaysApprove_NeverRequires()
    {
        var plan = MakePlan(SafeStep(), DangerousStep());
        Assert.False(PlanApprovalGate.RequiresApproval(plan, PlanApprovalMode.AlwaysApprove));
    }

    [Fact]
    public void AlwaysAsk_AlwaysRequires()
    {
        var plan = MakePlan(SafeStep());
        Assert.True(PlanApprovalGate.RequiresApproval(plan, PlanApprovalMode.AlwaysAsk));
    }

    [Fact]
    public void ApproveDestructive_SafeOnlyPlan_NoApproval()
    {
        // A plan with only safe tools and few steps doesn't require approval.
        var plan = MakePlan(SafeStep());
        Assert.False(PlanApprovalGate.RequiresApproval(plan, PlanApprovalMode.ApproveDestructive));
    }

    [Fact]
    public void ApproveDestructive_DangerousPlan_RequiresApproval()
    {
        var plan = MakePlan(SafeStep(), DangerousStep());
        Assert.True(PlanApprovalGate.RequiresApproval(plan, PlanApprovalMode.ApproveDestructive));
    }

    [Fact]
    public void ApproveDestructive_EscalationPlan_RequiresApproval()
    {
        var plan = MakePlan(SafeStep(), EscalationStep());
        Assert.True(PlanApprovalGate.RequiresApproval(plan, PlanApprovalMode.ApproveDestructive));
    }
}
