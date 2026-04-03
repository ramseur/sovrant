using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Core;

/// <summary>Executes a shell command and returns its combined stdout/stderr output.</summary>
public sealed class BashTool : ITool
{
    private const int DefaultTimeoutMs = 120_000;

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
        var command = GetString(input, "command");
        if (string.IsNullOrWhiteSpace(command))
            return "Error: command is required.";

        var timeoutMs = GetInt(input, "timeout", DefaultTimeoutMs);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();

        try
        {
            var shell = OperatingSystem.IsWindows() ? "bash.exe" : "/bin/bash";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = $"-c \"{EscapeArg(command)}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutSb.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderrSb.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var stdout = stdoutSb.ToString();
            var stderr = stderrSb.ToString();
            var exitCode = process.ExitCode;

            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(stdout)) result.Append(stdout);
            if (!string.IsNullOrEmpty(stderr)) result.Append(CultureInfo.InvariantCulture, $"[stderr]\n{stderr}");
            if (exitCode != 0) result.Append(CultureInfo.InvariantCulture, $"\n[exit code: {exitCode}]");

            return result.Length > 0 ? result.ToString() : string.Create(CultureInfo.InvariantCulture, $"[exit code: {exitCode}]");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return $"Error: command timed out after {timeoutMs} ms.";
        }
        catch (InvalidOperationException ex) { return $"Error starting process: {ex.Message}"; }
    }

    private static string EscapeArg(string arg) => arg.Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static int GetInt(JsonElement el, string prop, int def) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : def;

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
