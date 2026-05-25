using Sovrant.Agents.Swarm;
using Sovrant.Agents.Swarm.Bus;

namespace Sovrant.Agents.Tests.Swarm;

public sealed class RouteResolverTests
{
    private static readonly SwarmExecutionContext _ctx = new(WorkspaceId: "ws-abc", UserId: "u-1");

    [Fact]
    public void Silo_ReturnsNull()
    {
        var route = RouteResolver.Resolve("swarm-1", _ctx, SwarmFederationMode.Silo, "sovrant.swarm");
        Assert.Null(route);
    }

    [Fact]
    public void Federated_ReturnsWorkspaceAndSwarmSegments()
    {
        var route = RouteResolver.Resolve("swarm-1", _ctx, SwarmFederationMode.Federated, "sovrant.swarm");
        Assert.Equal("sovrant.swarm.ws-abc.swarm-1", route);
    }

    [Fact]
    public void ManagerLed_UsesParentSwarmId()
    {
        var route = RouteResolver.Resolve("child-1", _ctx, SwarmFederationMode.ManagerLed, "sovrant.swarm", parentSwarmId: "parent-99");
        Assert.Equal("sovrant.swarm.manager.parent-99.child-1", route);
    }

    [Fact]
    public void ManagerLed_NullParent_FallsBackToUnknown()
    {
        var route = RouteResolver.Resolve("child-1", _ctx, SwarmFederationMode.ManagerLed, "sovrant.swarm", parentSwarmId: null);
        Assert.Equal("sovrant.swarm.manager.unknown.child-1", route);
    }

    [Fact]
    public void Sanitizes_Slashes_In_SwarmId()
    {
        var route = RouteResolver.Resolve("ws/abc/swarm", _ctx, SwarmFederationMode.Federated, "sovrant.swarm");
        Assert.DoesNotContain('/', route!);
    }

    [Fact]
    public void EmptyWorkspace_FallsBackToDefault()
    {
        var ctx = new SwarmExecutionContext(WorkspaceId: null);
        var route = RouteResolver.Resolve("swarm-1", ctx, SwarmFederationMode.Federated, "sovrant.swarm");
        Assert.StartsWith("sovrant.swarm.default.", route);
    }
}
