using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Sovrant.Api.Types;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Tools.Mcp;

/// <summary>
/// Dynamically proxies a tool call to any connected MCP server at execution time,
/// without requiring the tool to be statically registered at startup.
/// </summary>
/// <remarks>
/// Use this when the model discovers a tool via <c>ToolSearch</c> or
/// <c>ListMcpResources</c> that isn't pre-registered in the tool registry.
/// </remarks>
public sealed class McpProxyTool : ITool
{
    private static readonly ToolDefinition s_definition = new("MCPTool", CreateSchema())
    {
        Description = "Calls a tool on a connected MCP server dynamically. " +
                      "Use the 'server' parameter to target a specific server, or omit it to search all connected servers. " +
                      "Use ToolSearch or ListMcpResources to discover available tools before calling.",
    };

    private readonly McpClientRegistry _registry;

    public McpProxyTool(McpClientRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        if (!_registry.HasClients)
            return "No MCP servers connected.";

        var toolName = GetString(input, "tool");
        if (string.IsNullOrWhiteSpace(toolName))
            return "The 'tool' parameter is required.";

        var serverName = GetString(input, "server");

        // Extract the nested 'input' object (arguments for the target tool).
        var toolInput = input.TryGetProperty("input", out var inputProp)
            && inputProp.ValueKind == JsonValueKind.Object
            ? inputProp
            : JsonDocument.Parse("{}").RootElement;

        var arguments = BuildArguments(toolInput);

        if (!string.IsNullOrWhiteSpace(serverName))
        {
            // Target a specific server.
            var match = _registry.Clients
                .FirstOrDefault(kv => kv.Key.Equals(serverName, StringComparison.OrdinalIgnoreCase));

            if (match.Value is null)
                return $"No MCP server named '{serverName}' is connected.";

            return await CallToolAsync(match.Value, toolName, arguments, ct).ConfigureAwait(false);
        }

        // No server specified — try each client and return the first success.
        var errors = new List<string>();
        foreach (var (name, client) in _registry.Clients)
        {
            try
            {
                return await CallToolAsync(client, toolName, arguments, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{name}: {ex.Message}");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Tool '{toolName}' not found or failed on all connected MCP servers:");
        foreach (var err in errors)
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {err}");
        return sb.ToString().TrimEnd();
    }

    private static async Task<string> CallToolAsync(
        ModelContextProtocol.Client.McpClient client,
        string toolName,
        Dictionary<string, object?> arguments,
        CancellationToken ct)
    {
        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: ct)
            .ConfigureAwait(false);

        if (result.Content is null || result.Content.Count == 0)
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            result.Content.OfType<TextContentBlock>().Select(c => c.Text ?? string.Empty));
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

    private static string GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "server": {
                    "type": "string",
                    "description": "Name of the connected MCP server to call. Omit to try all connected servers."
                },
                "tool": {
                    "type": "string",
                    "description": "Name of the tool to call on the MCP server."
                },
                "input": {
                    "type": "object",
                    "description": "Input arguments to pass to the tool. Structure depends on the target tool's schema.",
                    "additionalProperties": true
                }
            },
            "required": ["tool"]
        }
        """).RootElement;
}
