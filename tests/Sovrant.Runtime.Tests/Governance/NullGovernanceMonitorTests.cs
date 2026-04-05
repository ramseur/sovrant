using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class NullGovernanceMonitorTests
{
    [Fact]
    public async Task EvaluateAsync_AlwaysReturnsAllowed()
    {
        var monitor = new NullGovernanceMonitor();
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "rm -rf /");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }
}
