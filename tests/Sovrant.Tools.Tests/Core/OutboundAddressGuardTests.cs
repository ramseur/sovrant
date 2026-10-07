using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Sovrant.Tools.Core;

namespace Sovrant.Tools.Tests.Core;

/// <summary>
/// WebFetch must not reach the server's own network. The old check only looked at URLs that spelled
/// out an IP, so a hostname resolving to a private address, "localhost." or a redirect got through.
/// The connection is now checked on the address actually connected to.
/// </summary>
public sealed class OutboundAddressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]   // cloud metadata
    [InlineData("100.64.0.1")]        // carrier-grade NAT
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.1")]   // IPv4 written as IPv6
    [InlineData("::ffff:127.0.0.1")]
    public void Private_And_Local_Addresses_Are_Blocked(string address) =>
        Assert.True(OutboundAddressGuard.IsBlocked(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("140.82.112.3")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_Addresses_Are_Allowed(string address) =>
        Assert.False(OutboundAddressGuard.IsBlocked(IPAddress.Parse(address)));

    [Theory]
    [InlineData("http://localhost./")]
    [InlineData("http://app.localhost/")]
    [InlineData("http://[::ffff:169.254.169.254]/latest/meta-data/")]
    [InlineData("file:///etc/passwd")]
    public void Urls_That_Name_Local_Addresses_Are_Refused_Up_Front(string url) =>
        Assert.True(OutboundAddressGuard.IsBlockedUri(new Uri(url)));

    [Fact]
    public async Task A_Hostname_That_Resolves_To_A_Local_Address_Is_Refused_At_Connect()
    {
        // A server listening on loopback stands in for an internal service. "localhost." isn't caught by
        // a plain string compare; the connect check resolves it and refuses.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var client = new HttpClient(OutboundAddressGuard.CreateHandler());
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri($"http://localhost.:{port}/")));
            Assert.Contains("private or local address", ex.ToString(), StringComparison.Ordinal);
            Assert.False(listener.Pending()); // never connected
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task WebFetch_Refuses_Local_Hostnames()
    {
        var tool = new WebFetchTool(new Factory());
        var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"url":"http://localhost./admin"}""").RootElement);
        Assert.Equal(OutboundAddressGuard.BlockedMessage, result);
    }

    private sealed class Factory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(OutboundAddressGuard.CreateHandler());
    }
}
