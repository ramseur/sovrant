using System.Text.Json;
using Sovrant.Runtime.Config;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// Parses the standard <c>mcpServers</c> JSON block used by Claude Desktop, the
/// Anthropic CLI, and most MCP host docs. Accepts either:
/// <list type="bullet">
///   <item><description>A bare server object: <c>{ "command": "npx", "args": [...] }</c> or <c>{ "url": "...", "headers": {...} }</c></description></item>
///   <item><description>A single named entry: <c>{ "myname": { ... } }</c></description></item>
///   <item><description>A wrapped block: <c>{ "mcpServers": { "name": { ... } } }</c></description></item>
/// </list>
/// </summary>
public static class McpJsonParser
{
    /// <summary>Parses one or more MCP server entries from the supplied JSON.</summary>
    /// <param name="json">Raw JSON in any of the supported shapes.</param>
    /// <param name="defaultName">Name to use when the JSON is a bare server object with no name.</param>
    /// <exception cref="JsonException">Malformed JSON, or no recognisable entries.</exception>
    public static IReadOnlyDictionary<string, McpServerConfig> Parse(string json, string defaultName = "server")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Expected a JSON object.");

        // Unwrap { "mcpServers": { ... } } if present.
        if (root.TryGetProperty("mcpServers", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
            root = wrapped;

        var result = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal);

        // Bare server object (has command or url at top level)?
        if (LooksLikeServerObject(root))
        {
            result[defaultName] = ReadServer(root);
            return result;
        }

        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;
            result[prop.Name] = ReadServer(prop.Value);
        }

        if (result.Count == 0)
            throw new JsonException("No MCP server entries found.");

        return result;
    }

    private static bool LooksLikeServerObject(JsonElement el) =>
        el.TryGetProperty("command", out _) || el.TryGetProperty("url", out _);

    private static McpServerConfig ReadServer(JsonElement el)
    {
        // HTTP transport when URL is present.
        if (el.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
        {
            var urlStr = urlEl.GetString()!;
            if (!Uri.TryCreate(urlStr, UriKind.Absolute, out var url))
                throw new JsonException($"Invalid url: '{urlStr}'.");

            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            if (el.TryGetProperty("headers", out var hEl) && hEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var h in hEl.EnumerateObject())
                {
                    if (h.Value.ValueKind == JsonValueKind.String)
                        headers[h.Name] = h.Value.GetString() ?? string.Empty;
                }
            }

            return new McpServerConfig { Url = url, Headers = headers };
        }

        // Stdio transport.
        var command = el.TryGetProperty("command", out var cmdEl) && cmdEl.ValueKind == JsonValueKind.String
            ? cmdEl.GetString() ?? string.Empty
            : string.Empty;

        var args = new List<string>();
        if (el.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in argsEl.EnumerateArray())
            {
                if (a.ValueKind == JsonValueKind.String)
                    args.Add(a.GetString() ?? string.Empty);
            }
        }

        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (el.TryGetProperty("env", out var envEl) && envEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var e in envEl.EnumerateObject())
            {
                if (e.Value.ValueKind == JsonValueKind.String)
                    env[e.Name] = e.Value.GetString() ?? string.Empty;
            }
        }

        if (string.IsNullOrEmpty(command))
            throw new JsonException("Server entry must have either 'url' or 'command'.");

        return new McpServerConfig { Command = command, Args = args, Env = env };
    }
}
