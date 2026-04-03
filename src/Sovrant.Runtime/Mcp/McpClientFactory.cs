using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Sovrant.Runtime.Config;

namespace Sovrant.Runtime.Mcp;

/// <summary>Creates MCP client connections using stdio transports.</summary>
public sealed class SovrantMcpClientFactory : IMcpClientFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public SovrantMcpClientFactory(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    /// <inheritdoc/>
    public async Task<McpClient> CreateAsync(string name, McpServerConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var transportOptions = new StdioClientTransportOptions
        {
            Command = config.Command,
            Arguments = [.. config.Args],
            Name = name,
            EnvironmentVariables = config.Env.Count > 0
                ? config.Env.ToDictionary(kv => kv.Key, kv => (string?)kv.Value, StringComparer.Ordinal)
                : null,
        };

        var transport = new StdioClientTransport(transportOptions);
        return await McpClient.CreateAsync(transport, loggerFactory: _loggerFactory, cancellationToken: ct)
            .ConfigureAwait(false);
    }
}
