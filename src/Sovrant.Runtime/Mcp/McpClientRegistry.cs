using ModelContextProtocol.Client;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// Tracks all connected MCP clients by their server name.
/// Populated by <see cref="McpToolRegistrar"/> during initialization.
/// </summary>
public sealed class McpClientRegistry
{
    private readonly Dictionary<string, McpClient> _clients = new(StringComparer.Ordinal);

    /// <summary>Registers a connected client under the given server name.</summary>
    public void Register(string serverName, McpClient client) =>
        _clients[serverName] = client;

    /// <summary>Returns all registered (server name, client) pairs.</summary>
    public IReadOnlyDictionary<string, McpClient> Clients => _clients;

    /// <summary>Returns true when at least one client is registered.</summary>
    public bool HasClients => _clients.Count > 0;
}
