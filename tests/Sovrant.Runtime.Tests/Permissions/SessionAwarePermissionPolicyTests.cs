using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Tests.Permissions;

/// <summary>
/// Phase 145 — on a shared Web server each conversation is judged by its owner's permission mode,
/// and changing the mode (including EnterPlanMode/ExitPlanMode) affects that conversation only.
/// </summary>
public sealed class SessionAwarePermissionPolicyTests
{
    [Fact]
    public void Each_Conversation_Uses_Its_Own_Mode()
    {
        var policy = new SessionAwarePermissionPolicy(PermissionMode.Default);
        var alice = new SessionConfig { PermissionMode = PermissionMode.BypassPermissions };
        var bob = new SessionConfig { PermissionMode = PermissionMode.Plan };

        using (SessionContext.Push(alice))
            Assert.Equal(PolicyDecision.Allow, policy.Evaluate("Bash", isDestructive: true));
        using (SessionContext.Push(bob))
            Assert.NotEqual(PolicyDecision.Allow, policy.Evaluate("Bash", isDestructive: true));
    }

    [Fact]
    public void Setting_The_Mode_Changes_Only_The_Current_Conversation()
    {
        var policy = new SessionAwarePermissionPolicy(PermissionMode.Default);
        var alice = new SessionConfig();
        var bob = new SessionConfig();

        using (SessionContext.Push(alice))
            policy.Mode = PermissionMode.Plan; // e.g. EnterPlanMode in Alice's chat

        Assert.Equal(PermissionMode.Plan, alice.PermissionMode);
        Assert.Null(bob.PermissionMode);
        Assert.Equal(PermissionMode.Default, policy.Mode); // outside a conversation: the install default

        policy.Mode = PermissionMode.BypassPermissions; // no conversation: changes nobody
        Assert.Equal(PermissionMode.Default, policy.Mode);
    }
}
