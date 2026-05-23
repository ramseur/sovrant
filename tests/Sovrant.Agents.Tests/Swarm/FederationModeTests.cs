using Sovrant.Agents.Swarm;
using Sovrant.Agents.Swarm.Bus;

namespace Sovrant.Agents.Tests.Swarm;

/// <summary>Tests that SwarmFederationMode and SwarmFederationConfig behave as expected.</summary>
public sealed class FederationModeTests
{
    [Fact]
    public void DefaultSwarmConfig_HasNullFederation()
    {
        var config = new SwarmConfig();
        Assert.Null(config.Federation);
    }

    [Fact]
    public void SwarmFederationConfig_DefaultMode_IsSilo()
    {
        var cfg = new SwarmFederationConfig();
        Assert.Equal(SwarmFederationMode.Silo, cfg.Mode);
    }

    [Fact]
    public void SwarmFederationConfig_CanSetManagerLed()
    {
        var cfg = new SwarmFederationConfig
        {
            Mode = SwarmFederationMode.ManagerLed,
            ParentSwarmId = "parent-42",
        };

        Assert.Equal(SwarmFederationMode.ManagerLed, cfg.Mode);
        Assert.Equal("parent-42", cfg.ParentSwarmId);
    }

    [Fact]
    public void OpenClawConfig_DefaultServerName_IsOpenclaw()
    {
        var cfg = new OpenClawConfig();
        Assert.Equal("openclaw", cfg.ServerName);
    }

    [Fact]
    public void OpenClawConfig_DefaultRoutePrefix_IsSovrantSwarm()
    {
        var cfg = new OpenClawConfig();
        Assert.Equal("sovrant.swarm", cfg.RoutePrefix);
    }
}
