using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sovrant.Hosting;

namespace Sovrant.Server.Tests;

/// <summary>Phase 144 (GitHub #33) — SOVRANT_TRUSTED_PROXIES decides whose X-Forwarded-* headers count.</summary>
public sealed class ForwardedHeadersSetupTests
{
    private static ForwardedHeadersOptions Options(string? trustedProxies)
    {
        var services = new ServiceCollection();
        ForwardedHeadersSetup.Configure(services, trustedProxies);
        return services.BuildServiceProvider().GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }

    [Fact]
    public void Default_Trusts_Loopback_Only()
    {
        var o = Options(null);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost, o.ForwardedHeaders);
        Assert.Contains(IPAddress.IPv6Loopback, o.KnownProxies);
        Assert.DoesNotContain(o.KnownProxies, p => p.Equals(IPAddress.Parse("10.0.0.5")));
    }

    [Fact]
    public void Ips_And_Networks_Are_Added_And_Junk_Ignored()
    {
        var o = Options("172.18.0.0/16, 10.0.0.5, not-an-ip");
        Assert.Contains(o.KnownProxies, p => p.Equals(IPAddress.Parse("10.0.0.5")));
        Assert.Contains(o.KnownIPNetworks, n => n.Contains(IPAddress.Parse("172.18.4.2")));
    }

    [Fact]
    public void Star_Trusts_Any_Sender()
    {
        var o = Options("*");
        Assert.Empty(o.KnownProxies);
        Assert.Empty(o.KnownIPNetworks);
    }
}
