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
    private const int OutputCapChars = 256 * 1024;

    private static readonly string[] s_dangerousEnvVars =
    [
        "LD_PRELOAD",
        "DYLD_INSERT_LIBRARIES",
        "DYLD_LIBRARY_PATH",
        "LD_LIBRARY_PATH",
        "LD_AUDIT",
        "LD_DEBUG",
    ];

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
        var stdoutTruncated = false;
        var stderrTruncated = false;

        try
        {
            var shell = OperatingSystem.IsWindows() ? "bash.exe" : "/bin/bash";
            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = $"-c \"{EscapeArg(command)}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var envVar in s_dangerousEnvVars)
                psi.EnvironmentVariables.Remove(envVar);

            using var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                if (stdoutSb.Length < OutputCapChars) stdoutSb.AppendLine(e.Data);
                else stdoutTruncated = true;
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                if (stderrSb.Length < OutputCapChars) stderrSb.AppendLine(e.Data);
                else stderrTruncated = true;
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var stdout = stdoutSb.ToString();
            if (stdoutTruncated) stdout += "\n[stdout truncated at 256 KB]";
            var stderr = stderrSb.ToString();
            if (stderrTruncated) stderr += "\n[stderr truncated at 256 KB]";
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
