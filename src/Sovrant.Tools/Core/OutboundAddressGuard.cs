using System.Net;
using System.Net.Sockets;

namespace Sovrant.Tools.Core;

/// <summary>
/// Keeps agent-driven web requests (WebFetch) off the server's own network: loopback, private and
/// link-local ranges (including the cloud metadata endpoint, 169.254.169.254), carrier-grade NAT,
/// multicast and reserved addresses.
///
/// The check runs when the connection is made, on the address actually being connected to — so it
/// covers hostnames that resolve to private addresses, every redirect hop and DNS rebinding, not just
/// URLs that spell out an IP. A connection to a configured web proxy is allowed (the proxy is the
/// network boundary then).
/// </summary>
public static class OutboundAddressGuard
{
    public const string BlockedMessage =
        "Error: URL is blocked — requests to private/local addresses and non-HTTP(S) schemes are not allowed.";

    /// <summary>A handler for the WebFetch HTTP client that refuses connections to blocked addresses.</summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        ConnectCallback = ConnectAsync,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    };

    public static bool IsBlocked(IPAddress ip)
    {
        ArgumentNullException.ThrowIfNull(ip);
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] switch
            {
                0 => true,                                  // "this network"
                10 => true,                                 // private
                127 => true,                                // loopback
                100 => b[1] is >= 64 and <= 127,            // carrier-grade NAT
                169 => b[1] == 254,                         // link-local, cloud metadata
                172 => b[1] is >= 16 and <= 31,             // private
                192 => (b[1] == 168)                        // private
                       || (b[1] == 0 && b[2] == 0),         // IETF protocol assignments
                198 => b[1] is 18 or 19,                    // benchmarking
                >= 224 => true,                             // multicast, reserved, broadcast
                _ => false,
            };
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.Equals(IPAddress.IPv6Any) || IPAddress.IsLoopback(ip)) return true;
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || ip.IsIPv6UniqueLocal) return true;
            return false;
        }

        return true; // anything else isn't a normal internet address
    }

    /// <summary>Early, friendly check on the URL itself (scheme, "localhost", literal addresses).</summary>
    public static bool IsBlockedUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return true;

        var host = uri.IdnHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IsBlocked(ip);
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var endpoint = context.DnsEndPoint;
        var target = context.InitialRequestMessage.RequestUri;
        // Connecting to something other than the URL's host means a configured proxy: allowed.
        var viaProxy = target is not null && !string.Equals(target.IdnHost.TrimEnd('.'), endpoint.Host.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);

        var addresses = IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(endpoint.Host, ct).ConfigureAwait(false);
        if (!viaProxy && (addresses.Length == 0 || addresses.Any(IsBlocked)))
            throw new HttpRequestException($"{endpoint.Host} resolves to a private or local address, which isn't allowed.");

#pragma warning disable CA2000 // ownership passes to the NetworkStream (ownsSocket), disposed on failure
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
#pragma warning restore CA2000
        try
        {
            socket.NoDelay = true;
            // Connect to the addresses just checked, not a fresh lookup (which could answer differently).
            await socket.ConnectAsync(addresses, endpoint.Port, ct).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
