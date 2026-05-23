using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Agents.Swarm.Bus;

/// <summary>
/// Thin facade over <see cref="McpClientRegistry"/> that calls the nine standard
/// OpenClaw MCP tools: publish, subscribe, request_approval, approve, reject,
/// load_history, list_routes, tail_route, route_stats.
/// </summary>
public sealed class OpenClawBusClient
{
    private readonly McpClientRegistry _registry;

    public OpenClawBusClient(McpClientRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>
    /// Publishes a JSON payload to the named OpenClaw route.
    /// No-ops silently if the OpenClaw server is not connected.
    /// </summary>
    public Task PublishAsync(
        string serverName,
        string route,
        string payloadJson,
        CancellationToken ct = default)
    {
        var client = GetClient(serverName);
        if (client is null) return Task.CompletedTask;

        return CallAsync(client, "publish", new Dictionary<string, object?>
        {
            ["route"] = route,
            ["payload"] = payloadJson,
        }, ct);
    }

    /// <summary>
    /// Requests human or manager-agent approval for a swarm action.
    /// Returns the raw response text from OpenClaw (JSON with approved/rejected/pending).
    /// </summary>
    public Task<string> RequestApprovalAsync(
        string serverName,
        string route,
        string requestId,
        string description,
        CancellationToken ct = default)
    {
        var client = GetClient(serverName);
        if (client is null) return Task.FromResult(string.Empty);

        return CallAsync(client, "request_approval", new Dictionary<string, object?>
        {
            ["route"] = route,
            ["request_id"] = requestId,
            ["description"] = description,
        }, ct);
    }

    /// <summary>Loads the event history for a route (last <paramref name="limit"/> messages).</summary>
    public Task<string> LoadHistoryAsync(
        string serverName,
        string route,
        int limit = 50,
        CancellationToken ct = default)
    {
        var client = GetClient(serverName);
        if (client is null) return Task.FromResult(string.Empty);

        return CallAsync(client, "load_history", new Dictionary<string, object?>
        {
            ["route"] = route,
            ["limit"] = (object?)limit,
        }, ct);
    }

    /// <summary>Returns a listing of all routes visible to this OpenClaw instance.</summary>
    public Task<string> ListRoutesAsync(string serverName, CancellationToken ct = default)
    {
        var client = GetClient(serverName);
        if (client is null) return Task.FromResult(string.Empty);

        return CallAsync(client, "list_routes", [], ct);
    }

    private McpClient? GetClient(string serverName) =>
        _registry.Clients.TryGetValue(serverName, out var client) ? client : null;

    private static async Task<string> CallAsync(
        McpClient client,
        string toolName,
        Dictionary<string, object?> args,
        CancellationToken ct)
    {
        var result = await client.CallToolAsync(toolName, args, cancellationToken: ct)
            .ConfigureAwait(false);

        if (result.Content is null || result.Content.Count == 0)
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            result.Content.OfType<TextContentBlock>().Select(c => c.Text ?? string.Empty));
    }
}
