using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Tests.Permissions;

/// <summary>Tests for <see cref="ModeAwarePermissionPolicy"/>.</summary>
public sealed class ModeAwarePermissionPolicyTests
{
    [Theory]
    [InlineData(PermissionMode.BypassPermissions)]
    [InlineData(PermissionMode.DontAsk)]
    public void Evaluate_BypassModes_AllowDestructiveTools(PermissionMode mode)
    {
        var policy = new ModeAwarePermissionPolicy(mode);
        Assert.Equal(PolicyDecision.Allow, policy.Evaluate("bash", isDestructive: true));
    }

    [Fact]
    public void Evaluate_PlanMode_DeniesDestructiveTools()
    {
        var policy = new ModeAwarePermissionPolicy(PermissionMode.Plan);
        Assert.Equal(PolicyDecision.Deny, policy.Evaluate("write_file", isDestructive: true));
    }

    [Fact]
    public void Evaluate_PlanMode_AllowsReadTools()
    {
        var policy = new ModeAwarePermissionPolicy(PermissionMode.Plan);
        Assert.Equal(PolicyDecision.Allow, policy.Evaluate("read_file", isDestructive: false));
    }

    [Fact]
    public void Evaluate_AcceptEdits_AllowsFileEditTools()
    {
        var policy = new ModeAwarePermissionPolicy(PermissionMode.AcceptEdits);
        Assert.Equal(PolicyDecision.Allow, policy.Evaluate("write_file", isDestructive: true));
    }

    [Fact]
    public void Evaluate_AcceptEdits_RequiresConfirmationForBash()
    {
        var policy = new ModeAwarePermissionPolicy(PermissionMode.AcceptEdits);
        Assert.Equal(PolicyDecision.RequireConfirmation, policy.Evaluate("bash", isDestructive: true));
    }

    [Fact]
    public void Evaluate_Default_RequiresConfirmationForDestructiveTool()
    {
        var policy = new ModeAwarePermissionPolicy(PermissionMode.Default);
        Assert.Equal(PolicyDecision.RequireConfirmation, policy.Evaluate("bash", isDestructive: true));
    }

    [Fact]
    public void Evaluate_Default_AllowsNonDestructiveTool()
    {
        var policy = new ModeAwarePermissionPolicy(PermissionMode.Default);
        Assert.Equal(PolicyDecision.Allow, policy.Evaluate("read_file", isDestructive: false));
    }

    [Theory]
    [InlineData("bash", true)]
    [InlineData("write_file", true)]
    [InlineData("edit_file", true)]
    [InlineData("read_file", false)]
    [InlineData("list_directory", false)]
    public void IsDestructive_ReturnsExpected(string tool, bool expected)
    {
        Assert.Equal(expected, ModeAwarePermissionPolicy.IsDestructive(tool));
    }
}
