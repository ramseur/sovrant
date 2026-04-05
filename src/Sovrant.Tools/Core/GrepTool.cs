using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Core;

/// <summary>Searches file contents for a regular-expression pattern.</summary>
public sealed class GrepTool : ITool
{
    private const int MaxMatchLines = 500;

    private static readonly ToolDefinition s_definition = new("Grep", CreateSchema())
    {
        Description =
            "Searches file contents using a regular expression. " +
            "output_mode can be \"files_with_matches\" (default), \"content\" (matching lines), or \"count\". " +
            "Use glob to restrict to specific file types (e.g. \"*.cs\").",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var pattern = input.GetStringProp("pattern");
        if (string.IsNullOrWhiteSpace(pattern))
            return "Error: pattern is required.";

        var searchPath = input.GetStringProp("path", Directory.GetCurrentDirectory());
        var glob = input.GetStringProp("glob", "**/*");
        var outputMode = input.GetStringProp("output_mode", "files_with_matches");
        var caseInsensitive = input.GetBoolProp("case_insensitive", false);

        if (!Directory.Exists(searchPath) && !File.Exists(searchPath))
            return $"Error: path not found: {searchPath}";

        ct.ThrowIfCancellationRequested();

        Regex regex;
        try
        {
            var options = caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;
            regex = new Regex(pattern, options | RegexOptions.Compiled, TimeSpan.FromSeconds(5));
        }
        catch (ArgumentException ex)
        {
            return $"Error: invalid regex pattern: {ex.Message}";
        }

        var files = File.Exists(searchPath)
            ? [searchPath]
            : GetMatchingFiles(searchPath, glob);

        var results = new StringBuilder();
        var matchCount = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fileLines = await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false);
                var fileMatched = false;

                for (var i = 0; i < fileLines.Length; i++)
                {
                    if (!regex.IsMatch(fileLines[i])) continue;

                    fileMatched = true;
                    matchCount++;

                    if (outputMode == "content" && matchCount <= MaxMatchLines)
                        results.AppendLine(CultureInfo.InvariantCulture, $"{file}:{i + 1}: {fileLines[i]}");

                    if (matchCount > MaxMatchLines) break;
                }

                if (fileMatched && outputMode == "files_with_matches")
                    results.AppendLine(file);

                if (outputMode == "count" && fileMatched)
                    results.AppendLine(CultureInfo.InvariantCulture, $"{file}: {regex.Count(string.Join("\n", fileLines))}");
            }
            catch (IOException) { /* skip unreadable files */ }
            catch (UnauthorizedAccessException) { /* skip inaccessible files */ }
        }

        if (results.Length == 0) return "No matches found.";
        if (matchCount > MaxMatchLines) results.AppendLine(CultureInfo.InvariantCulture, $"\n[Truncated at {MaxMatchLines} matches]");

        return results.ToString().TrimEnd();
    }

    private static IEnumerable<string> GetMatchingFiles(string root, string globPattern)
    {
        var matcher = new Matcher();
        matcher.AddInclude(globPattern);
        var dirInfo = new DirectoryInfoWrapper(new DirectoryInfo(root));
        return matcher.Execute(dirInfo).Files.Select(f => Path.Combine(root, f.Path));
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "pattern":         {"type": "string", "description": "Regular expression to search for."},
                "path":            {"type": "string", "description": "File or directory to search (defaults to cwd)."},
                "glob":            {"type": "string", "description": "Glob filter for file types, e.g. \"*.cs\"."},
                "output_mode":     {"type": "string", "description": "\"files_with_matches\", \"content\", or \"count\"."},
                "case_insensitive":{"type": "boolean","description": "Case-insensitive matching (default false)."}
            },
            "required": ["pattern"]
        }
        """).RootElement;
}
