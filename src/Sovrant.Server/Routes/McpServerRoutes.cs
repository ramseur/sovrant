using Sovrant.Runtime.Mcp;
using Sovrant.Server.Auth;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers <c>GET /v1/mcp/servers</c> — a thin name-and-status listing the
/// chat UI uses to populate its Connections dropdown. Detailed config (commands,
/// headers, env) stays out of this surface; the Integrations page is the source
/// of truth for editing.
/// <para>
/// 2.0 (Phase 139 over HTTP): each server also reports its connection <c>state</c>
/// and, when unavailable, the friendly <c>message</c>, failure <c>kind</c> and next
/// automatic retry; <c>POST /v1/mcp/servers/{name}/retry</c> (admin) retries now.
/// </para>
/// </summary>
internal static class McpServerRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/mcp/servers", HandleAsync);
        app.MapPost("/v1/mcp/servers/{name}/retry", RetryAsync);
    }

    private static async Task<IResult> HandleAsync(
        IMcpServerStore store,
        McpClientRegistry clientRegistry,
        McpServerStatusRegistry statusRegistry,
        CancellationToken ct)
    {
        var configs = await store.GetAllAsync(ct).ConfigureAwait(false);
        var connected = clientRegistry.Clients;

        var servers = configs.Keys
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => Describe(n, connected.ContainsKey(n), statusRegistry.Get(n)))
            .ToList();

        return Results.Json(new { servers });
    }

    private static async Task<IResult> RetryAsync(
        string name,
        HttpContext ctx,
        IMcpServerStore store,
        McpToolRegistrar registrar,
        McpClientRegistry clientRegistry,
        CancellationToken ct)
    {
        if (!HttpContextAuthExtensions.IsAdmin(ctx))
            return Results.Json(new { error = "Forbidden." }, statusCode: StatusCodes.Status403Forbidden);
        var configs = await store.GetAllAsync(ct).ConfigureAwait(false);
        if (!configs.TryGetValue(name, out var config))
            return Results.NotFound(new { error = $"MCP server '{name}' not found" });

        var status = await registrar.RetryAsync(name, config, ct).ConfigureAwait(false);
        return Results.Json(Describe(name, clientRegistry.Clients.ContainsKey(name), status));
    }

    private static object Describe(string name, bool isConnected, McpServerStatus? status) => new
    {
        name,
        connected = isConnected,
        // connected | connecting | unavailable; servers never tried yet report connected/unavailable from the client registry
        state = status?.State switch
        {
            McpServerState.Connecting => "connecting",
            McpServerState.Unavailable => "unavailable",
            McpServerState.Connected => "connected",
            _ => isConnected ? "connected" : "unavailable",
        },
        message = status?.Failure is null ? null : status.Message,
        kind = status?.Failure?.Kind.ToString().ToLowerInvariant(),
        retrying = status?.NextRetryAt is not null,
        next_retry_at = status?.NextRetryAt,
    };
}
