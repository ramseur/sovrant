using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Core;

/// <summary>Writes content to a file, creating parent directories as needed.</summary>
public sealed class WriteFileTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Write", CreateSchema())
    {
        Description =
            "Writes content to a file at the specified path. " +
            "Creates the file and any necessary parent directories if they do not exist. " +
            "Overwrites the file if it already exists.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var filePath = GetString(input, "file_path");
        if (string.IsNullOrWhiteSpace(filePath))
            return "Error: file_path is required.";

        if (!input.TryGetProperty("content", out var contentProp))
            return "Error: content is required.";

        var content = contentProp.GetString() ?? string.Empty;

        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
            var lineCount = content.Split('\n').Length;
            return $"File written: {filePath} ({lineCount} lines)";
        }
        catch (IOException ex) { return $"Error writing file: {ex.Message}"; }
        catch (UnauthorizedAccessException ex) { return $"Error: access denied: {ex.Message}"; }
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "file_path": {"type": "string", "description": "Absolute path to the file to write."},
                "content":   {"type": "string", "description": "The content to write to the file."}
            },
            "required": ["file_path", "content"]
        }
        """).RootElement;
}
