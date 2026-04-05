using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Tools;

namespace Sovrant.Tools.Skills;

/// <summary>
/// Searches the registered tool definitions by keyword and returns a summary table.
/// Useful for discovering what tools are available without reading all tool definitions.
/// </summary>
public sealed class ToolSearchTool : ITool
{
    private static readonly ToolDefinition s_definition = new("ToolSearch", CreateSchema())
    {
        Description = "Lists or searches registered tools by keyword. " +
                      "Returns tool names and descriptions that match the query.",
    };

    private readonly IToolRegistry _registry;

    public ToolSearchTool(IToolRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var query = input.GetStringProp("query").Trim();

        var definitions = _registry.GetDefinitions();

        IEnumerable<ToolDefinition> matches = string.IsNullOrEmpty(query)
            ? definitions
            : definitions.Where(d =>
                d.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (d.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));

        var list = matches.OrderBy(d => d.Name).ToList();

        if (list.Count == 0)
            return Task.FromResult(string.IsNullOrEmpty(query)
                ? "No tools registered."
                : $"No tools found matching '{query}'.");

        var sb = new StringBuilder();
        var header = string.IsNullOrEmpty(query)
            ? $"Found {list.Count} tool(s):"
            : $"Found {list.Count} tool(s) matching '{query}':";
        sb.AppendLine(CultureInfo.InvariantCulture, $"{header}");
        sb.AppendLine();
        foreach (var d in list)
        {
            var desc = string.IsNullOrEmpty(d.Description) ? "(no description)" : d.Description;
            sb.AppendLine(CultureInfo.InvariantCulture, $"• {d.Name}: {desc}");
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }


    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "query": {"type": "string", "description": "Keyword to search tool names/descriptions. Omit to list all tools."}
            },
            "required": []
        }
        """).RootElement;
}
