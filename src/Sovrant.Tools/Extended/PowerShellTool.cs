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
        var command = input.GetStringProp("command");
        if (string.IsNullOrWhiteSpace(command))
            return "Error: command is required.";

        // Guard against excessively large commands that would OOM when base64-encoded.
        const int MaxCommandLength = 512 * 1024; // 512 KB
        if (command.Length > MaxCommandLength)
            return $"Error: command too large ({command.Length} chars). Maximum is {MaxCommandLength}.";

        var timeoutMs = input.GetIntProp("timeout", DefaultTimeoutMs);
        var shell = FindPowerShell();

        // Use -EncodedCommand with Base64 to avoid all shell escaping issues.
        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        try
        {
            var result = await ProcessExecutor.RunAsync(
                shell, ["-NonInteractive", "-EncodedCommand", encodedCommand],
                timeoutMs, ct: ct).ConfigureAwait(false);
            return result.Output;
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
