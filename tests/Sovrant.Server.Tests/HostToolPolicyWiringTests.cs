using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Tools;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 145 stopgap on Sovrant.Server: the member tool policy is registered, reads the ambient
/// principal (set per request by the bearer-token middleware and per workflow by the scheduler),
/// and the tool executor is built with it.
/// </summary>
public sealed class HostToolPolicyWiringTests(SovrantWebAppFactory factory) : IClassFixture<SovrantWebAppFactory>
{
    [Fact]
    public void Server_Blocks_Member_File_Tools_And_Allows_Admins()
    {
        var policy = factory.Services.GetRequiredService<IHostToolPolicy>();

        using (AmbientPrincipal.Push("member@example.com", "user"))
            Assert.Equal(HostToolAccess.MemberBlockedMessage, policy.GetBlockReason("Bash"));
        using (AmbientPrincipal.Push("admin@example.com", "admin"))
            Assert.Null(policy.GetBlockReason("Bash"));
        Assert.Null(policy.GetBlockReason("WebSearch"));
    }
}
