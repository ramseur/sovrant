using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Lsp;

namespace Sovrant.Tools.Lsp;

/// <summary>
/// Finds all references to a symbol at a given position.
/// Sends <c>textDocument/references</c> to the appropriate language server.
/// </summary>
public sealed class LspReferencesTool : ITool
{
    private static readonly ToolDefinition s_definition = new("LspReferences", CreateSchema())
    {
        Description =
            "Finds all locations that reference a symbol at a given position. " +
            "Returns file paths and line numbers for every reference, including the declaration.",
    };

    private readonly ILspClientManager _manager;

    public LspReferencesTool(ILspClientManager manager) => _manager = manager;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var filePath = input.GetStringProp("file_path");
        if (string.IsNullOrWhiteSpace(filePath))
            return "Error: file_path is required.";

        var line = input.GetIntProp("line");
        var character = input.GetIntProp("character");

        var client = _manager.GetClientForFile(filePath);
        if (client is null)
            return $"Error: no language server configured for {Path.GetExtension(filePath)} files.";

        if (!client.IsRunning)
            return $"Error: language server for {client.Language} is not running.";

        var locations = await client.ReferencesAsync(filePath, line, character, ct).ConfigureAwait(false);
        if (locations.Count == 0)
            return "No references found at this position.";

        var sb = new StringBuilder();
        sb.Append(System.Globalization.CultureInfo.InvariantCulture,
            $"Found {locations.Count} reference(s):").AppendLine();
        foreach (var loc in locations)
        {
            var path = LspClient.UriToPath(loc.Uri);
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"  {path}:{loc.Range.Start.Line + 1}:{loc.Range.Start.Character + 1}").AppendLine();
        }
        return sb.ToString().TrimEnd();
    }



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
