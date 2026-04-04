using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Lsp;

namespace Sovrant.Tools.Lsp;

/// <summary>
/// Returns the file and line where a symbol is defined.
/// Sends <c>textDocument/definition</c> to the appropriate language server.
/// </summary>
public sealed class LspDefinitionTool : ITool
{
    private static readonly ToolDefinition s_definition = new("LspDefinition", CreateSchema())
    {
        Description =
            "Finds the definition of a symbol at a given position. " +
            "Returns the file path and line number where the symbol is defined.",
    };

    private readonly ILspClientManager _manager;

    public LspDefinitionTool(ILspClientManager manager) => _manager = manager;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var filePath = GetString(input, "file_path");
        if (string.IsNullOrWhiteSpace(filePath))
            return "Error: file_path is required.";

        var line = GetInt(input, "line");
        var character = GetInt(input, "character");

        var client = _manager.GetClientForFile(filePath);
        if (client is null)
            return $"Error: no language server configured for {Path.GetExtension(filePath)} files.";

        if (!client.IsRunning)
            return $"Error: language server for {client.Language} is not running.";

        var locations = await client.DefinitionAsync(filePath, line, character, ct).ConfigureAwait(false);
        if (locations.Count == 0)
            return "No definition found at this position.";

        var sb = new StringBuilder();
        foreach (var loc in locations)
        {
            var path = LspClient.UriToPath(loc.Uri);
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"{path}:{loc.Range.Start.Line + 1}:{loc.Range.Start.Character + 1}").AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var i) ? i : 0;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "file_path": {
                    "type": "string",
                    "description": "Absolute path to the file."
                },
                "line": {
                    "type": "integer",
                    "description": "Zero-based line number."
                },
                "character": {
                    "type": "integer",
                    "description": "Zero-based character offset within the line."
                }
            },
            "required": ["file_path", "line", "character"]
        }
        """).RootElement;
}
