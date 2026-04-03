using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sovrant.Api.Types;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Mcp;

/// <summary>Discovers tools from MCP servers and registers them in the tool registry.</summary>
public sealed partial class McpToolRegistrar : IAsyncDisposable
{
    private readonly IMcpClientFactory _clientFactory;
    private readonly IToolRegistry _registry;
    private readonly ILogger<McpToolRegistrar> _logger;
    private readonly List<McpClient> _clients = [];

    [LoggerMessage(Level = LogLevel.Information, Message = "Registering {Count} tools from MCP server '{ServerName}'")]
    private static partial void LogRegistering(ILogger logger, int count, string serverName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to connect to MCP server '{ServerName}'")]
    private static partial void LogConnectionFailed(ILogger logger, string serverName, Exception ex);

    public McpToolRegistrar(
        IMcpClientFactory clientFactory,
        IToolRegistry registry,
        ILogger<McpToolRegistrar> logger)
    {
        _clientFactory = clientFactory;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Connects to each configured MCP server, lists its tools, and registers them
    /// in the tool registry with handlers that forward calls back to the server.
    /// </summary>
    public async Task RegisterAllAsync(
        IReadOnlyDictionary<string, McpServerConfig> servers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(servers);

        foreach (var (name, config) in servers)
        {
            try
            {
                var client = await _clientFactory.CreateAsync(name, config, ct).ConfigureAwait(false);
                _clients.Add(client);
                await RegisterFromClientAsync(name, client, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                LogConnectionFailed(_logger, name, ex);
            }
            catch (IOException ex)
            {
                LogConnectionFailed(_logger, name, ex);
            }
        }
    }

    private async Task RegisterFromClientAsync(string serverName, McpClient client, CancellationToken ct)
    {
        var tools = await client.ListToolsAsync(cancellationToken: ct).ConfigureAwait(false);
        LogRegistering(_logger, tools.Count, serverName);

        foreach (var tool in tools)
        {
            var capturedClient = client;
            var capturedName = tool.Name;

            JsonElement inputSchema;
            if (tool.JsonSchema is JsonElement schemaElem)
                inputSchema = schemaElem;
            else
                inputSchema = JsonDocument.Parse("{}").RootElement;

            var definition = new ToolDefinition(tool.Name, inputSchema) { Description = tool.Description };

            _registry.Register(definition, async (input, innerCt) =>
            {
                var args = BuildArguments(input);
                var result = await capturedClient.CallToolAsync(capturedName, args, cancellationToken: innerCt)
                    .ConfigureAwait(false);

                if (result.Content is null || result.Content.Count == 0)
                    return string.Empty;

                var parts = result.Content
                    .OfType<TextContentBlock>()
                    .Select(c => c.Text ?? string.Empty);

                return string.Join(Environment.NewLine, parts);
            });
        }
    }

    private static Dictionary<string, object?> BuildArguments(JsonElement input)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (input.ValueKind != JsonValueKind.Object)
            return dict;

        foreach (var prop in input.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number when prop.Value.TryGetInt64(out var l) => (object?)l,
                JsonValueKind.Number => prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.GetRawText(),
            };
        }

        return dict;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
            await client.DisposeAsync().ConfigureAwait(false);
        _clients.Clear();
    }
}
