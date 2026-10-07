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
    public void Server_Blocks_File_Tools_For_Everyone_By_Default_And_Hides_Them()
    {
        var policy = factory.Services.GetRequiredService<IHostToolPolicy>();

        using (AmbientPrincipal.Push("member@example.com", "user"))
            Assert.Equal(HostToolAccess.HostBlockedMessage, policy.GetBlockReason("Bash"));
        using (AmbientPrincipal.Push("admin@example.com", "admin"))
        {
            Assert.Equal(HostToolAccess.HostBlockedMessage, policy.GetBlockReason("Bash"));
            var names = factory.Services.GetRequiredService<IToolRegistry>().GetDefinitions().Select(d => d.Name).ToList();
            Assert.DoesNotContain("Bash", names);
            Assert.DoesNotContain("Write", names);
            Assert.Contains("WebSearch", names);
        }
        Assert.Null(policy.GetBlockReason("WebSearch"));
    }
}
