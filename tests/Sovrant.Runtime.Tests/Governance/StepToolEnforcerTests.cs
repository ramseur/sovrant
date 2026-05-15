using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class StepToolEnforcerTests
{
    [Fact]
    public void NullStep_AllowsAll()
    {
        Assert.True(StepToolEnforcer.IsAllowed("Bash", currentStep: null));
    }

    [Fact]
    public void NullAllowedTools_AllowsAll()
    {
        var step = new RuntimeStep(0, "Read config", "Config contents", RuntimeModelTier.Fast, AllowedTools: null);
        Assert.True(StepToolEnforcer.IsAllowed("Bash", step));
    }

    [Fact]
    public void EmptyAllowedTools_AllowsAll()
    {
        var step = new RuntimeStep(0, "Read config", "Config contents", RuntimeModelTier.Fast, AllowedTools: []);
        Assert.True(StepToolEnforcer.IsAllowed("Bash", step));
    }

    [Fact]
    public void AllowedToolInList_Allowed()
    {
        var step = new RuntimeStep(0, "Read config", "Config contents", RuntimeModelTier.Fast,
            AllowedTools: ["Read", "Grep"]);

        Assert.True(StepToolEnforcer.IsAllowed("Read", step));
        Assert.True(StepToolEnforcer.IsAllowed("Grep", step));
    }

    [Fact]
    public void ToolNotInList_Blocked()
    {
        var step = new RuntimeStep(0, "Read config", "Config contents", RuntimeModelTier.Fast,
            AllowedTools: ["Read", "Grep"]);

        Assert.False(StepToolEnforcer.IsAllowed("Write", step));
        Assert.False(StepToolEnforcer.IsAllowed("Bash", step));
    }

    [Fact]
    public void AllowedTools_CaseInsensitive()
    {
        var step = new RuntimeStep(0, "Read config", "Config contents", RuntimeModelTier.Fast,
            AllowedTools: ["read", "grep"]);

        Assert.True(StepToolEnforcer.IsAllowed("Read", step));
        Assert.True(StepToolEnforcer.IsAllowed("GREP", step));
    }

    [Fact]
    public void DenialReason_IncludesContext()
    {
        var step = new RuntimeStep(0, "Read config", "Config contents", RuntimeModelTier.Fast,
            AllowedTools: ["Read", "Grep"]);

        var reason = StepToolEnforcer.DenialReason("Write", step);

        Assert.Contains("Write", reason);
        Assert.Contains("Read", reason);
        Assert.Contains("Grep", reason);
    }
}
