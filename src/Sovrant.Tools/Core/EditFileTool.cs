using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Core;

/// <summary>Performs an exact string replacement in a file.</summary>
public sealed class EditFileTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Edit", CreateSchema())
    {
        Description =
            "Performs an exact string replacement in a file. " +
            "The old_string must match exactly, including whitespace and line endings. " +
            "Set replace_all to true to replace every occurrence instead of just the first.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var filePath = GetString(input, "file_path");
        if (string.IsNullOrWhiteSpace(filePath))
            return "Error: file_path is required.";

        if (!input.TryGetProperty("old_string", out var oldProp))
            return "Error: old_string is required.";
        if (!input.TryGetProperty("new_string", out var newProp))
            return "Error: new_string is required.";

        var oldString = oldProp.GetString() ?? string.Empty;
        var newString = newProp.GetString() ?? string.Empty;
        var replaceAll = GetBool(input, "replace_all", false);

        if (!File.Exists(filePath))
            return $"Error: file not found: {filePath}";

        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

            if (!content.Contains(oldString, StringComparison.Ordinal))
                return $"Error: old_string not found in {filePath}";

            var updated = replaceAll
                ? content.Replace(oldString, newString, StringComparison.Ordinal)
                : ReplaceFirst(content, oldString, newString);

            await File.WriteAllTextAsync(filePath, updated, ct).ConfigureAwait(false);

            var count = replaceAll
                ? CountOccurrences(content, oldString)
                : 1;
            return $"Edited {filePath} — replaced {count} occurrence(s).";
        }
        catch (IOException ex) { return $"Error editing file: {ex.Message}"; }
        catch (UnauthorizedAccessException ex) { return $"Error: access denied: {ex.Message}"; }
    }

    private static string ReplaceFirst(string text, string search, string replace)
    {
        var pos = text.IndexOf(search, StringComparison.Ordinal);
        return pos < 0 ? text : string.Concat(text.AsSpan(0, pos), replace, text.AsSpan(pos + search.Length));
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }

        return count;
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static bool GetBool(JsonElement el, string prop, bool def) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "file_path":   {"type": "string",  "description": "Absolute path to the file."},
                "old_string":  {"type": "string",  "description": "Exact text to replace."},
                "new_string":  {"type": "string",  "description": "Replacement text."},
                "replace_all": {"type": "boolean", "description": "Replace all occurrences (default false)."}
            },
            "required": ["file_path", "old_string", "new_string"]
        }
        """).RootElement;
}
