using System.Net;
using System.Net.Sockets;

namespace Sovrant.Server;

/// <summary>
/// Shared SSRF guard used by any route that accepts a user-supplied URL.
/// Blocks requests that resolve to reserved/private IP ranges (127.x, 10.x,
/// 172.16–31.x, 192.168.x, 169.254.x) and unresolvable hosts.
/// </summary>
internal static class SsrfGuard
{
    internal static async Task<bool> IsReservedAddressAsync(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IPAddress.TryParse(host, out var ip))
            return IsReservedIp(ip);

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            return addresses.Any(IsReservedIp);
        }
        catch (SocketException)
        {
            // Unresolvable host — block to be safe.
            return true;
        }
    }

    private static bool IsReservedIp(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 127
                || bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)
                || bytes[0] == 0;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;

        return false;
    }
}
