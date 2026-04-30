using Microsoft.Extensions.Configuration;
using Sovrant.Api.Config;
using Sovrant.Api.Routing;

namespace Sovrant.Api.Tests.Config;

public sealed class RouterOptionsTests : IDisposable
{
    private readonly string? _origMode;
    private readonly string? _origStrategy;

    public RouterOptionsTests()
    {
        _origMode = Environment.GetEnvironmentVariable("ROUTER_MODE");
        _origStrategy = Environment.GetEnvironmentVariable("ROUTER_STRATEGY");
        Environment.SetEnvironmentVariable("ROUTER_MODE", null);
        Environment.SetEnvironmentVariable("ROUTER_STRATEGY", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ROUTER_MODE", _origMode);
        Environment.SetEnvironmentVariable("ROUTER_STRATEGY", _origStrategy);
    }

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private static IConfiguration ConfigWith(IDictionary<string, string?> kv) =>
        new ConfigurationBuilder().AddInMemoryCollection(kv).Build();

    [Fact]
    public void Resolve_WithNothing_ReturnsDefaults()
    {
        var opts = RouterOptions.Resolve(EmptyConfig());
        Assert.Equal(RouterMode.Smart, opts.Mode);
        Assert.Equal(RouterStrategy.Balanced, opts.Strategy);
    }

    [Fact]
    public void Resolve_FromEnvVars_TakesPrecedence()
    {
        Environment.SetEnvironmentVariable("ROUTER_MODE", "Fixed");
        Environment.SetEnvironmentVariable("ROUTER_STRATEGY", "Cost");
        var opts = RouterOptions.Resolve(ConfigWith(new Dictionary<string, string?>
        {
            ["Router:Mode"] = "Smart",
            ["Router:Strategy"] = "Latency",
        }));
        Assert.Equal(RouterMode.Fixed, opts.Mode);
        Assert.Equal(RouterStrategy.Cost, opts.Strategy);
    }

    [Fact]
    public void Resolve_FromConfig_WhenEnvAbsent()
    {
        var opts = RouterOptions.Resolve(ConfigWith(new Dictionary<string, string?>
        {
            ["Router:Mode"] = "Fixed",
            ["Router:Strategy"] = "Latency",
        }));
        Assert.Equal(RouterMode.Fixed, opts.Mode);
        Assert.Equal(RouterStrategy.Latency, opts.Strategy);
    }

    [Fact]
    public void Mode_And_Strategy_AreMutable()
    {
        var opts = new RouterOptions { Mode = RouterMode.Smart, Strategy = RouterStrategy.Balanced };
        opts.Mode = RouterMode.Fixed;
        opts.Strategy = RouterStrategy.Cost;
        Assert.Equal(RouterMode.Fixed, opts.Mode);
        Assert.Equal(RouterStrategy.Cost, opts.Strategy);
    }
}
