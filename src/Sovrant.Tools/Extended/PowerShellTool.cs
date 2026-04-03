using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Extended;

/// <summary>Executes a PowerShell command using pwsh or powershell.exe.</summary>
public sealed class PowerShellTool : ITool
{
    private const int DefaultTimeoutMs = 120_000;

    private static readonly ToolDefinition s_definition = new("PowerShell", CreateSchema())
    {
        Description =
            "Executes a PowerShell command using pwsh (PowerShell 7+) or powershell.exe. " +
            "Returns combined stdout and stderr. Primarily useful on Windows.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var command = GetString(input, "command");
        if (string.IsNullOrWhiteSpace(command))
            return "Error: command is required.";

        var timeoutMs = GetInt(input, "timeout", DefaultTimeoutMs);
        var shell = FindPowerShell();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = $"-NonInteractive -Command \"{command.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
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

            var sb = new StringBuilder();
            if (stdoutSb.Length > 0) sb.Append(stdoutSb);
            if (stderrSb.Length > 0) sb.Append(CultureInfo.InvariantCulture, $"[stderr]\n{stderrSb}");
            if (process.ExitCode != 0) sb.Append(CultureInfo.InvariantCulture, $"\n[exit code: {process.ExitCode}]");

            return sb.Length > 0 ? sb.ToString() : string.Create(CultureInfo.InvariantCulture, $"[exit code: {process.ExitCode}]");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return $"Error: command timed out after {timeoutMs} ms.";
        }
        catch (InvalidOperationException ex) { return $"Error starting PowerShell: {ex.Message}"; }
    }

    private static string FindPowerShell()
    {
        // Prefer pwsh (cross-platform PS7+), fall back to powershell.exe on Windows
        if (OperatingSystem.IsWindows())
        {
            var pwsh = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell", "7", "pwsh.exe");
            return File.Exists(pwsh) ? pwsh : "powershell.exe";
        }

        return "pwsh";
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static int GetInt(JsonElement el, string prop, int def) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "command":     {"type": "string",  "description": "PowerShell command or script to execute."},
                "description": {"type": "string",  "description": "Brief description for display."},
                "timeout":     {"type": "integer", "description": "Timeout in milliseconds (default 120000)."}
            },
            "required": ["command"]
        }
        """).RootElement;
}
