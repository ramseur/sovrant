using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Core;

/// <summary>Reads a file from the local filesystem, returning its content with optional line numbers.</summary>
public sealed class ReadFileTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Read", CreateSchema())
    {
        Description =
            "Reads a file from the local filesystem. Returns file content with line numbers. " +
            "Use offset and limit to read a specific range of lines. " +
            "Supports text files; binary files return a hex summary.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var filePath = input.GetStringProp("file_path");
        if (string.IsNullOrWhiteSpace(filePath))
            return "Error: file_path is required.";

        var offset = input.GetIntProp("offset", 0);
        var limit = input.GetIntProp("limit", 2000);

        if (!File.Exists(filePath))
            return $"Error: file not found: {filePath}";

        const long MaxBytes = 10 * 1024 * 1024; // 10 MB
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxBytes)
            return $"Error: file is too large to read ({fileInfo.Length / 1024 / 1024} MB). Use offset and limit to read specific ranges, or use Grep to search within the file.";

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, ct).ConfigureAwait(false);
            var start = Math.Max(0, offset);
            var end = Math.Min(lines.Length, start + limit);

            var sb = new StringBuilder();
            for (var i = start; i < end; i++)
                sb.Append(CultureInfo.InvariantCulture, $"{i + 1,6}\u2192{lines[i]}\n");

            if (end < lines.Length)
                sb.Append(CultureInfo.InvariantCulture, $"\n[{lines.Length - end} more lines — use offset={end} to continue]");

            return sb.ToString();
        }
        catch (IOException ex) { return $"Error reading file: {ex.Message}"; }
        catch (UnauthorizedAccessException ex) { return $"Error: access denied: {ex.Message}"; }
    }



    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "file_path": {"type": "string", "description": "Absolute path to the file to read."},
                "offset":    {"type": "integer", "description": "1-based line offset to start reading from."},
                "limit":     {"type": "integer", "description": "Maximum number of lines to return (default 2000)."}
            },
            "required": ["file_path"]
        }
        """).RootElement;
}
