using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Lsp;
using Sovrant.Lsp.Types;

namespace Sovrant.Tools.Lsp;

/// <summary>
/// Returns all diagnostics (errors, warnings) for a given file.
/// Reads from the language server's most recently published diagnostics.
/// </summary>
public sealed class LspDiagnosticsTool : ITool
{
    private static readonly ToolDefinition s_definition = new("LspDiagnostics", CreateSchema())
    {
        Description =
            "Returns all errors and warnings (diagnostics) in a file as reported by the language server. " +
            "Includes severity, line number, code, and message for each diagnostic.",
    };

    private readonly ILspClientManager _manager;

    public LspDiagnosticsTool(ILspClientManager manager) => _manager = manager;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var filePath = input.GetStringProp("file_path");
        if (string.IsNullOrWhiteSpace(filePath))
            return Task.FromResult("Error: file_path is required.");

        var client = _manager.GetClientForFile(filePath);
        if (client is null)
            return Task.FromResult($"Error: no language server configured for {Path.GetExtension(filePath)} files.");

        if (!client.IsRunning)
            return Task.FromResult($"Error: language server for {client.Language} is not running.");

        var diagnostics = client.GetDiagnostics(filePath);
        if (diagnostics.Count == 0)
            return Task.FromResult("No diagnostics for this file.");

        var sb = new StringBuilder();
        sb.Append(System.Globalization.CultureInfo.InvariantCulture,
            $"Found {diagnostics.Count} diagnostic(s):").AppendLine();
        foreach (var d in diagnostics)
        {
            var severity = DiagnosticSeverity.ToLabel(d.Severity);
            var code = d.Code is not null ? $" [{d.Code}]" : string.Empty;
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"  {severity}{code} L{d.Range.Start.Line + 1}:{d.Range.Start.Character + 1} — {d.Message}").AppendLine();
        }
        return Task.FromResult(sb.ToString().TrimEnd());
    }


    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "file_path": {
                    "type": "string",
                    "description": "Absolute path to the file."
                }
            },
            "required": ["file_path"]
        }
        """).RootElement;
}
