using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class GovernanceVerdictTests
{
    [Fact]
    public void Allowed_Singleton_IsAllow()
    {
        var verdict = GovernanceVerdict.Allowed;
        Assert.Equal(GovernanceAction.Allow, verdict.Action);
        Assert.Equal(string.Empty, verdict.Reason);
    }

    [Fact]
    public void Block_HasReasonAndRule()
    {
        var verdict = new GovernanceVerdict(GovernanceAction.Block, "test reason", "TestRule");
        Assert.Equal(GovernanceAction.Block, verdict.Action);
        Assert.Equal("test reason", verdict.Reason);
        Assert.Equal("TestRule", verdict.Rule);
    }

    [Fact]
    public void WithExpression_ChangesAction()
    {
        var verdict = new GovernanceVerdict(GovernanceAction.Block, "reason", "Rule");
        var downgraded = verdict with { Action = GovernanceAction.Warn };

        Assert.Equal(GovernanceAction.Warn, downgraded.Action);
        Assert.Equal("reason", downgraded.Reason);
        Assert.Equal("Rule", downgraded.Rule);
    }
}
