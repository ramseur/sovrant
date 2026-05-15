using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Sovrant.Api.Types;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Tools.Mcp;

/// <summary>
/// Lists resources available from connected MCP servers.
/// If a server name is provided, lists only that server's resources;
/// otherwise lists resources from all connected servers.
/// </summary>
public sealed class ListMcpResourcesTool : ITool
{
    private static readonly ToolDefinition s_definition = new("ListMcpResources", CreateSchema())
    {
        Description = "Lists resources available from connected MCP servers. " +
                      "Optionally filter by server name.",
    };

    private readonly McpClientRegistry _registry;

    public ListMcpResourcesTool(McpClientRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        if (!_registry.HasClients)
            return "No MCP servers connected.";

        var serverFilter = input.GetStringProp("server");

        var clients = string.IsNullOrWhiteSpace(serverFilter)
            ? _registry.Clients
            : _registry.Clients
                .Where(kv => kv.Key.Equals(serverFilter, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        if (clients.Count == 0)
            return $"No MCP server named '{serverFilter}' is connected.";

        var sb = new StringBuilder();
        foreach (var (name, client) in clients)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"── {name} ──────────────────────────────────");
            try
            {
                var resources = await client.ListResourcesAsync(cancellationToken: ct).ConfigureAwait(false);
                if (resources.Count == 0)
                {
                    sb.AppendLine("  (no resources)");
                }
                else
                {
                    foreach (var r in resources)
                    {
                        var desc = string.IsNullOrEmpty(r.Description) ? string.Empty : $" — {r.Description}";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"  {r.Uri}{desc}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  Error listing resources: {ex.Message}");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }


    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "server": {"type": "string", "description": "Optional MCP server name to filter by."}
            },
            "required": []
        }
        """).RootElement;
}
