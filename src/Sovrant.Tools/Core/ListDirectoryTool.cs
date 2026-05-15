using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Core;

/// <summary>Lists the contents of a directory in a tree-like format.</summary>
public sealed class ListDirectoryTool : ITool
{
    private const int MaxEntries = 500;

    private static readonly ToolDefinition s_definition = new("LS", CreateSchema())
    {
        Description =
            "Lists the files and directories at the given path. " +
            "Returns a structured listing showing files and subdirectories.",
    };

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var path = input.GetStringProp("path", Directory.GetCurrentDirectory());

        if (!Directory.Exists(path))
            return Task.FromResult(File.Exists(path)
                ? $"Error: '{path}' is a file, not a directory."
                : $"Error: directory not found: {path}");

        ct.ThrowIfCancellationRequested();

        var sb = new StringBuilder();
        sb.AppendLine(path);

        try
        {
            AppendEntries(sb, path, string.Empty, ref ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  [access denied: {ex.Message}]");
        }
        catch (DirectoryNotFoundException)
        {
            sb.AppendLine("  [directory no longer exists]");
        }
        catch (IOException ex)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  [IO error: {ex.Message}]");
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }

    private static void AppendEntries(StringBuilder sb, string dirPath, string indent, ref CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var entries = Directory.GetFileSystemEntries(dirPath)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .Take(MaxEntries)
            .ToList();

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            if (Directory.Exists(entry))
                sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}  {name}/");
            else
                sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}  {name}");
        }

        if (entries.Count >= MaxEntries)
            sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}  [listing truncated at {MaxEntries} entries]");
    }


    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "path": {"type": "string", "description": "Directory path to list (defaults to cwd)."}
            },
            "required": []
        }
        """).RootElement;
}
