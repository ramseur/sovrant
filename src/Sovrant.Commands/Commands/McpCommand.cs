using System.Globalization;
using System.Text;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Tools;

namespace Sovrant.Commands.Commands;

/// <summary>Lists connected MCP servers and their registered tools.</summary>
public sealed class McpCommand : ISlashCommand
{
    private readonly McpClientRegistry _mcp;
    private readonly IToolRegistry _tools;

    public McpCommand(McpClientRegistry mcp, IToolRegistry tools)
    {
        _mcp = mcp;
        _tools = tools;
    }

    public string Name => "mcp";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "List connected MCP servers and their tools.";
    public string Category => "Config";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var clients = _mcp.Clients;
        if (clients.Count == 0)
            return Task.FromResult(
                new SlashCommandResult("No MCP servers connected. Configure in sovrant.json."));

        var sb = new StringBuilder();

        // Get all tool definitions to identify MCP-proxied tools
        var allTools = _tools.GetDefinitions();

        foreach (var entry in clients.OrderBy(
            c => c.Key, StringComparer.OrdinalIgnoreCase))
        {
            var serverName = entry.Key;
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {serverName}");

            // MCP-proxied tools are registered with a "mcp_{serverName}_" prefix
            var prefix = $"mcp_{serverName}_";
            var serverTools = allTools
                .Where(t => t.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (serverTools.Count > 0)
            {
                foreach (var t in serverTools)
                {
                    // Strip the mcp_ prefix for display
                    var shortName = t.Name[prefix.Length..];
                    var desc = t.Description ?? "";
                    if (desc.Length > 45)
                        desc = string.Concat(desc.AsSpan(0, 42), "...");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    {shortName,-22} {desc}");
                }
            }
            else
            {
                sb.AppendLine("    (no tools registered)");
            }

            sb.AppendLine();
        }

        sb.Append(CultureInfo.InvariantCulture, $"{clients.Count} MCP server(s) connected.");

        return Task.FromResult(new SlashCommandResult(sb.ToString()));
    }
}
