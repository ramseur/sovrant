using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Extended;

/// <summary>Reads and edits Jupyter notebook (.ipynb) cells.</summary>
public sealed class NotebookEditTool : ITool
{
    private static readonly ToolDefinition s_definition = new("NotebookEdit", CreateSchema())
    {
        Description =
            "Reads or edits a Jupyter notebook (.ipynb) file. " +
            "edit_mode can be \"replace\" (replace a cell's source), \"insert\" (add a new cell), or \"delete\" (remove a cell). " +
            "Omit edit_mode to read the notebook. cell_number is 0-based.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var notebookPath = input.GetStringProp("notebook_path");
        if (string.IsNullOrWhiteSpace(notebookPath))
            return "Error: notebook_path is required.";

        var editModeEarly = input.GetStringProp("edit_mode");
        if (!File.Exists(notebookPath))
        {
            // Auto-create a blank notebook when inserting into a non-existent file.
            if (!string.Equals(editModeEarly, "insert", StringComparison.OrdinalIgnoreCase))
                return $"Error: file not found: {notebookPath}";

            var blank = new JsonObject
            {
                ["nbformat"] = 4,
                ["nbformat_minor"] = 5,
                ["metadata"] = new JsonObject
                {
                    ["kernelspec"] = new JsonObject
                    {
                        ["display_name"] = "Python 3",
                        ["language"] = "python",
                        ["name"] = "python3",
                    },
                    ["language_info"] = new JsonObject { ["name"] = "python" },
                },
                ["cells"] = new JsonArray(),
            };
            var dir = Path.GetDirectoryName(notebookPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(notebookPath,
                blank.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct).ConfigureAwait(false);
        }

        try
        {
            var json = await File.ReadAllTextAsync(notebookPath, ct).ConfigureAwait(false);
            var notebook = JsonNode.Parse(json) as JsonObject;
            if (notebook is null)
                return "Error: invalid notebook JSON.";

            var cells = notebook["cells"] as JsonArray;
            if (cells is null)
                return "Error: notebook has no cells array.";

            var editMode = input.GetStringProp("edit_mode");

            if (string.IsNullOrEmpty(editMode))
                return ReadNotebook(cells);

            var cellNumber = input.GetIntProp("cell_number", 0);
            var newSource = input.GetStringProp("new_source");
            var cellType = input.GetStringProp("cell_type", "code");

            if (string.Equals(editMode, "replace", StringComparison.OrdinalIgnoreCase))
            {
                if (cellNumber < 0 || cellNumber >= cells.Count)
                    return string.Create(CultureInfo.InvariantCulture, $"Error: cell_number {cellNumber} out of range (0–{cells.Count - 1}).");
                if (cells[cellNumber] is JsonObject cell)
                    cell["source"] = JsonValue.Create(newSource);
            }
            else if (string.Equals(editMode, "insert", StringComparison.OrdinalIgnoreCase))
            {
                var newCell = new JsonObject
                {
                    ["cell_type"] = cellType,
                    ["source"] = newSource,
                    ["metadata"] = new JsonObject(),
                    ["outputs"] = new JsonArray(),
                    ["execution_count"] = JsonValue.Create<int?>(null),
                };
                var insertAt = Math.Clamp(cellNumber, 0, cells.Count);
                cells.Insert(insertAt, newCell);
            }
            else if (string.Equals(editMode, "delete", StringComparison.OrdinalIgnoreCase))
            {
                if (cellNumber < 0 || cellNumber >= cells.Count)
                    return string.Create(CultureInfo.InvariantCulture, $"Error: cell_number {cellNumber} out of range (0–{cells.Count - 1}).");
                cells.RemoveAt(cellNumber);
            }
            else
            {
                return $"Error: unknown edit_mode '{editMode}'. Use replace, insert, or delete.";
            }

            await File.WriteAllTextAsync(notebookPath, notebook.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct)
                .ConfigureAwait(false);

            return string.Create(CultureInfo.InvariantCulture, $"Notebook updated: {notebookPath} ({editMode} at cell {cellNumber})");
        }
        catch (JsonException ex) { return $"Error parsing notebook: {ex.Message}"; }
        catch (IOException ex) { return $"Error reading/writing notebook: {ex.Message}"; }
    }

    private static string ReadNotebook(JsonArray cells)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i] is not JsonObject cell) continue;
            var type = cell["cell_type"]?.GetValue<string>() ?? "unknown";
            var source = cell["source"]?.GetValue<string>() ?? string.Empty;
            sb.AppendLine(CultureInfo.InvariantCulture, $"## Cell {i} [{type}]");
            sb.AppendLine(source);
            sb.AppendLine();
        }

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "(empty notebook)";
    }



    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "notebook_path": {"type": "string", "description": "Path to the .ipynb file."},
                "edit_mode":     {"type": "string", "description": "\"replace\", \"insert\", or \"delete\". Omit to read."},
                "cell_number":   {"type": "integer","description": "0-based cell index."},
                "new_source":    {"type": "string", "description": "New cell source (for replace/insert)."},
                "cell_type":     {"type": "string", "description": "Cell type for insert: \"code\" or \"markdown\"."}
            },
            "required": ["notebook_path"]
        }
        """).RootElement;
}
