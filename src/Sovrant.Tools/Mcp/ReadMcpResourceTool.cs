using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Sovrant.Api.Types;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Tools.Mcp;

/// <summary>
/// Reads a resource from a connected MCP server by URI.
/// </summary>
public sealed class ReadMcpResourceTool : ITool
{
    private static readonly ToolDefinition s_definition = new("ReadMcpResource", CreateSchema())
    {
        Description = "Reads a resource from a connected MCP server by URI. " +
                      "Use ListMcpResources to discover available URIs.",
    };

    private readonly McpClientRegistry _registry;

    public ReadMcpResourceTool(McpClientRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        if (!_registry.HasClients)
            return "No MCP servers connected.";

        var uriString = GetString(input, "uri");
        if (string.IsNullOrWhiteSpace(uriString))
            return "Error: uri is required.";

        if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            return $"Error: '{uriString}' is not a valid absolute URI.";

        var serverFilter = GetString(input, "server");

        // Find the right client: use server filter if given, else try all clients
        var candidates = string.IsNullOrWhiteSpace(serverFilter)
            ? _registry.Clients.Values.ToList()
            : _registry.Clients
                .Where(kv => kv.Key.Equals(serverFilter, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Value)
                .ToList();

        if (candidates.Count == 0)
            return string.IsNullOrWhiteSpace(serverFilter)
                ? "No MCP servers connected."
                : $"No MCP server named '{serverFilter}' is connected.";

        InvalidOperationException? lastError = null;
        foreach (var client in candidates)
        {
            try
            {
                var result = await client.ReadResourceAsync(uri, cancellationToken: ct).ConfigureAwait(false);
                if (result.Contents is null || result.Contents.Count == 0)
                    return "(empty resource)";

                var sb = new StringBuilder();
                foreach (var content in result.Contents)
                {
                    switch (content)
                    {
                        case TextResourceContents text:
                            sb.Append(text.Text);
                            break;
                        case BlobResourceContents blob:
                            sb.Append(CultureInfo.InvariantCulture,
                                $"[binary blob, {blob.DecodedData.Length} bytes, mime: {blob.MimeType ?? "unknown"}]");
                            break;
                        default:
                            sb.Append(content.ToString());
                            break;
                    }
                    sb.AppendLine();
                }

                return sb.ToString().TrimEnd();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                lastError = ex;
            }
        }

        return $"Error reading resource '{uriString}': {lastError?.Message ?? "unknown error"}";
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "uri":    {"type": "string", "description": "The resource URI to read (must be absolute)."},
                "server": {"type": "string", "description": "Optional MCP server name hint for routing."}
            },
            "required": ["uri"]
        }
        """).RootElement;
}
