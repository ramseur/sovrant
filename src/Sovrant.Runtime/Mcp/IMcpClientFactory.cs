using ModelContextProtocol.Client;
using Sovrant.Runtime.Config;

namespace Sovrant.Runtime.Mcp;

/// <summary>Creates MCP client connections to external tool servers.</summary>
public interface IMcpClientFactory
{
    /// <summary>Creates and connects an MCP client for the given server configuration.</summary>
    /// <param name="name">A display name for the server.</param>
    /// <param name="config">The server's connection configuration.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<McpClient> CreateAsync(string name, McpServerConfig config, CancellationToken ct = default);
}
