using Sovrant.Runtime.Mcp;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers <c>GET /v1/mcp/servers</c> — a thin name-and-status listing the
/// chat UI uses to populate its Connections dropdown. Detailed config (commands,
/// headers, env) stays out of this surface; the Integrations page is the source
/// of truth for editing.
/// </summary>
internal static class McpServerRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/v1/mcp/servers", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        IMcpServerStore store,
        McpClientRegistry clientRegistry,
        CancellationToken ct)
    {
        var configs = await store.GetAllAsync(ct).ConfigureAwait(false);
        var connected = clientRegistry.Clients;

        var servers = configs.Keys
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => new
            {
                name = n,
                connected = connected.ContainsKey(n),
            })
            .ToList();

        return Results.Json(new { servers });
    }
}
