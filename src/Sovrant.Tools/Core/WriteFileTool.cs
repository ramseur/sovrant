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

    // The Sovrant artifact store root — writes here must go through the Artifact tool.
    // Must match LocalArtifactStore's default (Sovrant.Runtime/Artifacts/LocalArtifactStore.cs).
    private static readonly string s_artifactsRoot = Path.GetFullPath(
        Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "workspaces"));

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var filePath = input.GetStringProp("file_path");
        if (string.IsNullOrWhiteSpace(filePath))
            return "Error: file_path is required.";

        if (!Path.IsPathRooted(filePath))
            return $"Error: file_path must be an absolute path. Got: '{filePath}'. " +
                   "Use the Artifact tool (action='write') to save documents, reports, and generated content.";

        // Prevent writes into the artifact store — those must go through the Artifact tool.
        var fullPath = Path.GetFullPath(filePath);
        if (fullPath.StartsWith(s_artifactsRoot, StringComparison.OrdinalIgnoreCase))
            return $"Error: cannot write directly to the artifact store at '{s_artifactsRoot}'. " +
                   "Use the Artifact tool (action='write') instead.";

        if (!input.TryGetProperty("content", out var contentProp))
            return "Error: content is required.";

        var content = contentProp.GetString() ?? string.Empty;

        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmpPath = filePath + ".tmp";
            await File.WriteAllTextAsync(tmpPath, content, ct).ConfigureAwait(false);
            File.Move(tmpPath, filePath, overwrite: true);
            var lineCount = content.Split('\n').Length;
            return $"File written: {filePath} ({lineCount} lines)";
        }
        catch (IOException ex) { return $"Error writing file: {ex.Message}"; }
        catch (UnauthorizedAccessException ex)
        {
            return OperatingSystem.IsWindows()
                ? $"Error: access denied writing {filePath}. Try restarting as Administrator. ({ex.Message})"
                : $"Error: access denied: {ex.Message}";
        }
    }


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
