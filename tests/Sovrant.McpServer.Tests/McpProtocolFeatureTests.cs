using System.Reflection;
using ModelContextProtocol.Protocol;

namespace Sovrant.McpServer.Tests;

/// <summary>Tests for MCP protocol features: prompts, completions, logging, resource subscriptions.</summary>
public sealed class McpProtocolFeatureTests
{
    private static readonly MethodInfo s_subscribe = typeof(McpServerSetup)
        .GetMethod("SubscribeToResource", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_unsubscribe = typeof(McpServerSetup)
        .GetMethod("UnsubscribeFromResource", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static EmptyResult Subscribe(string uri) =>
        (EmptyResult)s_subscribe.Invoke(null, [new SubscribeRequestParams { Uri = uri }])!;

    private static EmptyResult Unsubscribe(string uri) =>
        (EmptyResult)s_unsubscribe.Invoke(null, [new UnsubscribeRequestParams { Uri = uri }])!;

    // ── Resource Subscriptions ───────────────────────────────────────────────

    [Fact]
    public void Subscribe_IncrementsRefCount()
    {
        var uri = $"sovrant://sessions/{Guid.NewGuid():N}";
        Assert.Equal(0, McpServerSetup.GetSubscriptionCount(uri));

        Subscribe(uri);
        Assert.Equal(1, McpServerSetup.GetSubscriptionCount(uri));

        Subscribe(uri);
        Assert.Equal(2, McpServerSetup.GetSubscriptionCount(uri));
    }

    [Fact]
    public void Unsubscribe_DecrementsRefCount()
    {
        var uri = $"sovrant://sessions/{Guid.NewGuid():N}";
        Subscribe(uri);
        Subscribe(uri);
        Assert.Equal(2, McpServerSetup.GetSubscriptionCount(uri));

        Unsubscribe(uri);
        Assert.Equal(1, McpServerSetup.GetSubscriptionCount(uri));
    }

    [Fact]
    public void Unsubscribe_NeverGoesNegative()
    {
        var uri = $"sovrant://sessions/neg-{Guid.NewGuid():N}";
        Unsubscribe(uri);
        Assert.Equal(0, McpServerSetup.GetSubscriptionCount(uri));
    }

    [Fact]
    public void GetSubscriptionCount_UnknownUri_ReturnsZero()
    {
        Assert.Equal(0, McpServerSetup.GetSubscriptionCount("sovrant://nonexistent"));
    }
}
