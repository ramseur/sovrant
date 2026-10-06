using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace Sovrant.Hosting;

/// <summary>
/// Reverse-proxy support (GitHub #33): honour X-Forwarded-For / -Proto / -Host so the app sees the
/// real client IP and scheme (and HTTPS redirection doesn't loop behind a TLS-terminating proxy).
/// Only trusted proxies are believed: loopback by default; <c>SOVRANT_TRUSTED_PROXIES</c> adds a
/// comma-separated list of proxy IPs or CIDR networks (e.g. <c>172.18.0.0/16</c>), or <c>*</c> to
/// trust any sender (only safe when the app isn't reachable except through the proxy).
/// Used by both Sovrant.Web and Sovrant.Server.
/// </summary>
public static class ForwardedHeadersSetup
{
    public const string TrustedProxiesVariable = "SOVRANT_TRUSTED_PROXIES";

    public static void Configure(IServiceCollection services, string? trustedProxies)
    {
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            if (string.IsNullOrWhiteSpace(trustedProxies))
                return; // defaults: loopback proxies only

            if (trustedProxies.Trim() == "*")
            {
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
                return;
            }

            foreach (var entry in trustedProxies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (entry.Contains('/', StringComparison.Ordinal) && System.Net.IPNetwork.TryParse(entry, out var network))
                    o.KnownIPNetworks.Add(network);
                else if (IPAddress.TryParse(entry, out var address))
                    o.KnownProxies.Add(address);
            }
        });
    }
}
