using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Tests.Permissions;

/// <summary>Tests for <see cref="ModeAwarePermissionPolicy"/>.</summary>
public sealed class ModeAwarePermissionPolicyTests
{
    [Fact]
    public void Evaluate_BypassPermissions_AllowsDestructiveTools()
    {
        var policy = new ModeAwarePermissionPolicy(PermissionMode.BypassPermissions);
        Assert.Equal(PolicyDecision.Allow, policy.Evaluate("bash", isDestructive: true));
    }

    /// <summary>
    /// Phase 59 hardened DontAsk: Dangerous-tier tools now require confirmation
    /// unless SOVRANT_UNSAFE_DONTASK=true is set (for CI pipelines only).
    /// </summary>
    [Fact]
    public void Evaluate_DontAsk_RequiresConfirmationForDangerousTools()
    {
        var policy = new ModeAwarePermissionPolicy(PermissionMode.DontAsk);
        Assert.Equal(PolicyDecision.RequireConfirmation, policy.Evaluate("bash", isDestructive: true));
    }

    [Fact]
    public void Evaluate_DontAsk_AllowsSafeAndModerateTools()
    {
        var policy = new ModeAwarePermissionPolicy(PermissionMode.DontAsk);
        Assert.Equal(PolicyDecision.Allow, policy.Evaluate("read", isDestructive: false));
        Assert.Equal(PolicyDecision.Allow, policy.Evaluate("write", isDestructive: true));
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
