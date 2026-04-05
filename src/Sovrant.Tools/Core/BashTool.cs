using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Core;

/// <summary>Executes a shell command and returns its combined stdout/stderr output.</summary>
public sealed class BashTool : ITool
{
    private const int DefaultTimeoutMs = 120_000;
    private const int OutputCapChars = 256 * 1024;

    private static readonly ToolDefinition s_definition = new("Bash", CreateSchema())
    {
        Description =
            "Executes a bash/sh command and returns the combined stdout and stderr output. " +
            "Use the timeout parameter to control the maximum execution time in milliseconds. " +
            "Returns exit code, stdout, and stderr in a structured format.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var command = input.GetStringProp("command");
        if (string.IsNullOrWhiteSpace(command))
            return "Error: command is required.";

        var timeoutMs = input.GetIntProp("timeout", DefaultTimeoutMs);
        var shell = OperatingSystem.IsWindows() ? "bash.exe" : "/bin/bash";

        try
        {
            var result = await ProcessExecutor.RunWithTempFileAsync(
                shell, [], command, ".sh", timeoutMs, OutputCapChars, ct).ConfigureAwait(false);
            return result.Output;
        }
        catch (InvalidOperationException ex) { return $"Error starting process: {ex.Message}"; }
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "command":     {"type": "string",  "description": "The shell command to execute."},
                "description": {"type": "string",  "description": "Brief description for display."},
                "timeout":     {"type": "integer", "description": "Timeout in milliseconds (default 120000)."}
            },
            "required": ["command"]
        }
        """).RootElement;
}
