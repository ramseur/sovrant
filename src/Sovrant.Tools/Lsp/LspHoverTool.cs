using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Lsp;

namespace Sovrant.Tools.Lsp;

/// <summary>
/// Returns type info and documentation for a symbol at a given file position.
/// Sends <c>textDocument/hover</c> to the appropriate language server.
/// </summary>
public sealed class LspHoverTool : ITool
{
    private static readonly ToolDefinition s_definition = new("LspHover", CreateSchema())
    {
        Description =
            "Returns type information and documentation for a symbol at a given position in a file. " +
            "Requires a language server to be configured and running for the file's language.",
    };

    private readonly ILspClientManager _manager;

    public LspHoverTool(ILspClientManager manager) => _manager = manager;

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

        var result = await client.HoverAsync(filePath, line, character, ct).ConfigureAwait(false);
        if (result?.Contents is null)
            return "No hover information available at this position.";

        return result.Contents.Value;
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
