using System.Text.Json;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Core;

/// <summary>Finds files matching a glob pattern, sorted by modification time.</summary>
public sealed class GlobTool : ITool
{
    private static readonly ToolDefinition s_definition = new("Glob", CreateSchema())
    {
        Description =
            "Finds files matching a glob pattern (e.g. \"**/*.cs\", \"src/**/*.ts\"). " +
            "Results are sorted by modification time, most recent first. " +
            "Use the path parameter to restrict the search to a specific directory.",
    };

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var pattern = GetString(input, "pattern");
        if (string.IsNullOrWhiteSpace(pattern))
            return Task.FromResult("Error: pattern is required.");

        var searchPath = GetString(input, "path", Directory.GetCurrentDirectory());
        if (!Directory.Exists(searchPath))
            return Task.FromResult($"Error: directory not found: {searchPath}");

        ct.ThrowIfCancellationRequested();

        var matcher = new Matcher();
        matcher.AddInclude(pattern);

        var dirInfo = new DirectoryInfoWrapper(new DirectoryInfo(searchPath));
        var result = matcher.Execute(dirInfo);

        if (!result.HasMatches)
            return Task.FromResult("No files found.");

        var files = result.Files
            .Select(f => Path.Combine(searchPath, f.Path))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return Task.FromResult(string.Join(Environment.NewLine, files));
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "pattern": {"type": "string", "description": "Glob pattern, e.g. \"**/*.cs\"."},
                "path":    {"type": "string", "description": "Root directory to search (defaults to cwd)."}
            },
            "required": ["pattern"]
        }
        """).RootElement;
}
