using System.Collections.Concurrent;
using ModelContextProtocol.Client;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// Tracks all connected MCP clients by their server name.
/// Populated by <see cref="McpToolRegistrar"/> during initialization and by its background
/// reconnects (Phase 139), so both maps are concurrent.
/// </summary>
public sealed class McpClientRegistry
{
    private readonly ConcurrentDictionary<string, McpClient> _clients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _toolToServer = new(StringComparer.Ordinal);

    /// <summary>Registers a connected client under the given server name.</summary>
    public void Register(string serverName, McpClient client) =>
        _clients[serverName] = client;

    /// <summary>Removes the client registered under the given server name, if any.</summary>
    public void Unregister(string serverName) =>
        _clients.TryRemove(serverName, out _);

    /// <summary>Returns all registered (server name, client) pairs.</summary>
    public IReadOnlyDictionary<string, McpClient> Clients => _clients;

    /// <summary>Returns true when at least one client is registered.</summary>
    public bool HasClients => !_clients.IsEmpty;

    /// <summary>Records the server origin of a tool name. Last write wins on reconnect.</summary>
    public void MapTool(string toolName, string serverName) =>
        _toolToServer[toolName] = serverName;

    /// <summary>Removes all tool mappings whose origin is the given server.</summary>
    public void ForgetServerTools(string serverName)
    {
        var toRemove = _toolToServer
            .Where(kv => string.Equals(kv.Value, serverName, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var name in toRemove)
            _toolToServer.TryRemove(name, out _);
    }

    /// <summary>Tool name → originating MCP server name. Tools not in this map are non-MCP.</summary>
    public IReadOnlyDictionary<string, string> ToolToServer => _toolToServer;
}
