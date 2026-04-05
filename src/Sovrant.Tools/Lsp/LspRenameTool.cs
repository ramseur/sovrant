using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Lsp;

namespace Sovrant.Tools.Lsp;

/// <summary>
/// Performs a workspace-wide rename of a symbol with a preview of all affected files.
/// Sends <c>textDocument/rename</c> to the appropriate language server.
/// </summary>
public sealed class LspRenameTool : ITool
{
    private static readonly ToolDefinition s_definition = new("LspRename", CreateSchema())
    {
        Description =
            "Renames a symbol at a given position across the entire workspace. " +
            "Returns a preview of all files and edits that would be made. " +
            "The actual file changes are NOT applied — use Edit or Write tools to apply them.",
    };

    private readonly ILspClientManager _manager;

    public LspRenameTool(ILspClientManager manager) => _manager = manager;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var filePath = input.GetStringProp("file_path");
        if (string.IsNullOrWhiteSpace(filePath))
            return "Error: file_path is required.";

        var newName = input.GetStringProp("new_name");
        if (string.IsNullOrWhiteSpace(newName))
            return "Error: new_name is required.";

        var line = input.GetIntProp("line");
        var character = input.GetIntProp("character");

        var client = _manager.GetClientForFile(filePath);
        if (client is null)
            return $"Error: no language server configured for {Path.GetExtension(filePath)} files.";

        if (!client.IsRunning)
            return $"Error: language server for {client.Language} is not running.";

        var edit = await client.RenameAsync(filePath, line, character, newName, ct).ConfigureAwait(false);
        if (edit?.Changes is null || edit.Changes.Count == 0)
            return "Rename produced no changes. The symbol may not be renameable at this position.";

        var sb = new StringBuilder();
        var totalEdits = 0;
        foreach (var (uri, edits) in edit.Changes)
        {
            var path = LspClient.UriToPath(uri);
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"{path} ({edits.Length} edit(s)):").AppendLine();
            foreach (var e in edits)
            {
                sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                    $"  L{e.Range.Start.Line + 1}:{e.Range.Start.Character + 1}-" +
                    $"L{e.Range.End.Line + 1}:{e.Range.End.Character + 1} → \"{e.NewText}\"").AppendLine();
            }
            totalEdits += edits.Length;
        }

        sb.Insert(0, string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"Rename preview: {totalEdits} edit(s) across {edit.Changes.Count} file(s):\n"));
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
                },
                "new_name": {
                    "type": "string",
                    "description": "The new name for the symbol."
                }
            },
            "required": ["file_path", "line", "character", "new_name"]
        }
        """).RootElement;
}
