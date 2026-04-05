using System.Globalization;
using System.Text.Json;
using Spectre.Console;

namespace Sovrant.Cli;

/// <summary>
/// Renders structured unified diffs for file edit and write tool calls
/// using Spectre.Console markup (green for additions, red for removals).
/// </summary>
internal static class DiffRenderer
{
    private static readonly HashSet<string> s_editTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "edit", "edit_file", "str_replace",
    };

    private static readonly HashSet<string> s_writeTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "write", "write_file", "create", "create_file",
    };

    /// <summary>
    /// Returns true if this tool is a file-modifying tool that can show a diff.
    /// </summary>
    public static bool IsFileModifyTool(string toolName) =>
        s_editTools.Contains(toolName) || s_writeTools.Contains(toolName);

    /// <summary>
    /// Renders the tool input as a structured diff before execution.
    /// For edit tools: shows old_string → new_string as a unified diff.
    /// For write tools: shows the file path and a summary of content to be written.
    /// </summary>
    public static void RenderToolInput(string toolName, JsonElement input)
    {
        if (s_editTools.Contains(toolName))
            RenderEditDiff(input);
        else if (s_writeTools.Contains(toolName))
            RenderWriteSummary(input);
    }

    private static void RenderEditDiff(JsonElement input)
    {
        var filePath = TryGetString(input, "file_path") ?? TryGetString(input, "path") ?? "unknown";
        var oldStr = TryGetString(input, "old_string") ?? TryGetString(input, "old_str") ?? "";
        var newStr = TryGetString(input, "new_string") ?? TryGetString(input, "new_str") ?? "";

        AnsiConsole.MarkupLine($"[grey]  {Markup.Escape(filePath)}[/]");

        if (string.IsNullOrEmpty(oldStr) && string.IsNullOrEmpty(newStr))
            return;

        var oldLines = oldStr.Split('\n');
        var newLines = newStr.Split('\n');

        // Show removed lines in red, added lines in green.
        foreach (var line in oldLines)
        {
            AnsiConsole.MarkupLine($"[red]  - {Markup.Escape(TruncateLine(line))}[/]");
        }

        foreach (var line in newLines)
        {
            AnsiConsole.MarkupLine($"[green]  + {Markup.Escape(TruncateLine(line))}[/]");
        }
    }

    private static void RenderWriteSummary(JsonElement input)
    {
        var filePath = TryGetString(input, "file_path") ?? TryGetString(input, "path") ?? "unknown";
        var content = TryGetString(input, "content") ?? "";

        var lineCount = content.Split('\n').Length;
        var charCount = content.Length;

        AnsiConsole.MarkupLine($"[grey]  {Markup.Escape(filePath)}[/]");
        AnsiConsole.MarkupLine(string.Create(CultureInfo.InvariantCulture,
            $"[green]  + {lineCount} lines ({charCount} chars)[/]"));

        // Show first few lines as preview.
        var previewLines = content.Split('\n').Take(5);
        foreach (var line in previewLines)
        {
            AnsiConsole.MarkupLine($"[green dim]  + {Markup.Escape(TruncateLine(line))}[/]");
        }

        if (lineCount > 5)
            AnsiConsole.MarkupLine($"[grey]  ... ({lineCount - 5} more lines)[/]");
    }

    private static string? TryGetString(JsonElement el, string property)
    {
        if (el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(property, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static string TruncateLine(string line) =>
        line.Length > 120 ? string.Concat(line.AsSpan(0, 120), "...") : line;
}
