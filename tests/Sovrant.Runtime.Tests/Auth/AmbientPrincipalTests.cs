using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Tests.Auth;

/// <summary>
/// Phase 145 stopgap — the ambient principal Sovrant.Server sets per request and per scheduled
/// workflow, which the member tool policy reads.
/// </summary>
public sealed class AmbientPrincipalTests
{
    [Fact]
    public async Task Flows_Into_Work_Started_From_The_Request_Even_After_It_Ends()
    {
        Task<string?> background;
        var release = new TaskCompletionSource();
        using (AmbientPrincipal.Push("sam@example.com", "user"))
        {
            background = Task.Run(async () => { await release.Task; return AmbientPrincipal.Current?.UserId; });
        }
        Assert.Null(AmbientPrincipal.Current); // the request is over
        release.SetResult();
        Assert.Equal("sam@example.com", await background); // its background work still knows the user
    }

    [Fact]
    public void Push_Restores_The_Previous_Principal()
    {
        using (AmbientPrincipal.Push("admin@example.com", "admin"))
        {
            using (AmbientPrincipal.Push("sam@example.com", "user"))
                Assert.False(AmbientPrincipal.Accessor.IsAdmin);
            Assert.True(AmbientPrincipal.Accessor.IsAdmin);
        }
        Assert.Null(AmbientPrincipal.Accessor.UserId);
    }

    [Fact]
    public void Server_Policy_Blocks_Members_And_Unknown_Callers_But_Not_Admins()
    {
        var policy = new MemberHostToolPolicy(AmbientPrincipal.Accessor, settings: null);

        Assert.NotNull(policy.GetBlockReason("Bash")); // no principal at all
        using (AmbientPrincipal.Push("sam@example.com", "user"))
            Assert.NotNull(policy.GetBlockReason("Bash"));
        using (AmbientPrincipal.Push("admin@example.com", "admin"))
            Assert.Null(policy.GetBlockReason("Bash"));
    }
}
