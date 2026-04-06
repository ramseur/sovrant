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
            "Executes a shell command and returns the combined stdout and stderr output. " +
            "On Windows this uses PowerShell; on Linux/macOS it uses bash. " +
            "Use the timeout parameter to control the maximum execution time in milliseconds.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var command = input.GetStringProp("command");
        if (string.IsNullOrWhiteSpace(command))
            return "Error: command is required.";

        var timeoutMs = input.GetIntProp("timeout", DefaultTimeoutMs);

        try
        {
            ProcessExecutor.Result result;
            if (OperatingSystem.IsWindows())
            {
                // Use PowerShell on Windows to avoid requiring WSL.
                var shell = FindPowerShell();
                result = await ProcessExecutor.RunWithTempFileAsync(
                    shell, ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File"],
                    command, ".ps1", timeoutMs, OutputCapChars, ct).ConfigureAwait(false);
            }
            else
            {
                result = await ProcessExecutor.RunWithTempFileAsync(
                    "/bin/bash", [], command, ".sh", timeoutMs, OutputCapChars, ct).ConfigureAwait(false);
            }

            return result.Output;
        }
        catch (InvalidOperationException ex) { return $"Error starting process: {ex.Message}"; }
        catch (System.ComponentModel.Win32Exception ex) { return $"Error starting shell: {ex.Message}"; }
        catch (IOException ex) { return $"Error executing command: {ex.Message}"; }
    }

    /// <summary>Finds pwsh (PowerShell 7+) first, falls back to Windows PowerShell.</summary>
    private static string FindPowerShell()
    {
        // Prefer cross-platform PowerShell 7+ if installed.
        var pwshPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"),
            "pwsh.exe",
        };

        foreach (var p in pwshPaths)
        {
            if (Path.IsPathFullyQualified(p) && File.Exists(p))
                return p;
        }

        // Fall back to Windows PowerShell 5.1 (always present on Windows 10+).
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
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
