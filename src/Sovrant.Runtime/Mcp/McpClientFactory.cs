using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Sovrant.Runtime.Config;

namespace Sovrant.Runtime.Mcp;

/// <summary>Creates MCP client connections using stdio or HTTP transports.</summary>
/// <remarks>
/// Picks the transport from <see cref="McpServerConfig"/>: when <c>Url</c> is set,
/// connects via <see cref="HttpClientTransport"/> with any configured headers (e.g.
/// <c>Authorization: Bearer …</c>); otherwise falls back to stdio with command/args/env.
/// When <see cref="McpServerConfig.OAuthConfig"/> is present and the config already
/// contains an <c>Authorization</c> header (written by <see cref="McpOAuthService"/>
/// after a successful token exchange), that header is passed through automatically.
/// </remarks>
public sealed class SovrantMcpClientFactory : IMcpClientFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public SovrantMcpClientFactory(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    /// <inheritdoc/>
    public async Task<McpClient> CreateAsync(string name, McpServerConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        IClientTransport transport = config.Url is not null
            ? CreateHttpTransport(name, config)
            : CreateStdioTransport(name, config);

        return await McpClient.CreateAsync(transport, loggerFactory: _loggerFactory, cancellationToken: ct)
            .ConfigureAwait(false);
    }

    private static StdioClientTransport CreateStdioTransport(string name, McpServerConfig config)
    {
        var options = new StdioClientTransportOptions
        {
            Command = config.Command,
            Arguments = [.. config.Args],
            Name = name,
            EnvironmentVariables = config.Env.Count > 0
                ? config.Env.ToDictionary(kv => kv.Key, kv => (string?)kv.Value, StringComparer.Ordinal)
                : null,
        };
        return new StdioClientTransport(options);
    }

    private HttpClientTransport CreateHttpTransport(string name, McpServerConfig config)
    {
        var options = new HttpClientTransportOptions
        {
            Endpoint = config.Url!,
            Name = name,
        };
        if (config.Headers.Count > 0)
        {
            options.AdditionalHeaders = config.Headers.ToDictionary(
                kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        }
        return new HttpClientTransport(options, _loggerFactory);
    }
}
