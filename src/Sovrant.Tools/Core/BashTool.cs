using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Tools.Shell;

namespace Sovrant.Tools.Core;

/// <summary>Executes a shell command and returns its combined stdout/stderr output.</summary>
public sealed class BashTool : ITool
{
    private const int DefaultTimeoutMs = 120_000;
    private const int OutputCapChars = 256 * 1024;

    // Phase 43 — sentinel printed by the wrapped script after the user command
    // runs, carrying the shell's post-command cwd back to the parent. Parsed
    // out of stdout and used to update ShellSessionState so the next call
    // inherits the new cwd. The marker is unique enough that collisions with
    // normal output are vanishingly unlikely.
    private const string CwdSentinel = "__SOVRANT_CWD_SENTINEL__";

    private readonly ShellSessionState _sessionState;
    private readonly ShellEnvironment _shellEnv;

    private static readonly ToolDefinition s_definition = new("Bash", CreateSchema())
    {
        Description =
            "Executes a shell command and returns the combined stdout and stderr output. " +
            "On Windows this uses PowerShell; on Linux/macOS it uses bash. " +
            "The working directory persists between commands, but shell state does not. " +
            "Use the timeout parameter to control the maximum execution time in milliseconds.",
    };

    /// <summary>
    /// Parameterless constructor for test callers and code paths that don't
    /// participate in DI. Uses a fresh session state scoped to this instance,
    /// which is fine for isolated unit tests.
    /// </summary>
    public BashTool() : this(new ShellSessionState(), new ShellEnvironment()) { }

    public BashTool(ShellSessionState sessionState) : this(sessionState, new ShellEnvironment()) { }

    public BashTool(ShellSessionState sessionState, ShellEnvironment shellEnv)
    {
        _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
        _shellEnv = shellEnv ?? throw new ArgumentNullException(nameof(shellEnv));
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var command = input.GetStringProp("command");
        if (string.IsNullOrWhiteSpace(command))
            return "Error: command is required.";

        var timeoutMs = input.GetIntProp("timeout", DefaultTimeoutMs);
        var cwd = _sessionState.CurrentDirectory;

        // Verify the tracked cwd still exists before handing it to the shell.
        // If a previous command did `rm -rf` on it, fall back to the process
        // cwd rather than fail to spawn.
        if (!Directory.Exists(cwd))
        {
            cwd = Directory.GetCurrentDirectory();
            _sessionState.CurrentDirectory = cwd;
        }

        try
        {
            ProcessExecutor.Result result;
            if (OperatingSystem.IsWindows())
            {
                // PowerShell: append a Write-Output that emits the final cwd
                // after the user's command so we can capture `cd` effects.
                // Using `;` means the sentinel prints even if the user command
                // errors out; `$LASTEXITCODE` is preserved because the
                // Write-Output doesn't overwrite it (only external commands do).
                var wrapped =
                    command +
                    "\n" +
                    $"Write-Output ('{CwdSentinel}' + (Get-Location).Path)\n";

                var shell = _shellEnv.PowerShell?.Path
                    ?? ShellEnvironment.FindPowerShellExecutable()
                    ?? throw new InvalidOperationException("PowerShell not found on this system.");
                result = await ProcessExecutor.RunWithTempFileAsync(
                    shell, ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File"],
                    wrapped, ".ps1", timeoutMs, OutputCapChars,
                    workingDirectory: cwd, ct: ct).ConfigureAwait(false);
            }
            else
            {
                // Bash: same idea — echo the final cwd after the user command.
                // We wrap the user command in `{ ...; }` so a bare `exit N`
                // still lets us observe the cwd before the subshell dies...
                // except `exit` in the top-level script terminates before the
                // trailing echo. That's an acceptable corner case: if the user
                // explicitly exits the shell mid-command, they don't expect
                // cwd to persist. For the common case (`cd foo`, `cd ..`,
                // `pushd`, etc.) the sentinel fires as expected.
                var wrapped =
                    command +
                    "\n" +
                    $"printf '%s%s\\n' '{CwdSentinel}' \"$(pwd)\"\n";

                result = await ProcessExecutor.RunWithTempFileAsync(
                    "/bin/bash", [], wrapped, ".sh", timeoutMs, OutputCapChars,
                    workingDirectory: cwd, ct: ct).ConfigureAwait(false);
            }

            // Strip the sentinel line from the output and, if present, update
            // the tracked cwd. If the sentinel never fired (timeout, hard
            // exit, crashed shell), leave the cwd unchanged.
            var cleaned = ExtractCwdAndClean(result.Output);

            // Phase 43 #22 — detect the Windows PowerShell "running scripts is
            // disabled" error and append actionable guidance. Without this the
            // model sees a generic .ps1 failure and retries the same command.
            if (OperatingSystem.IsWindows() && LooksLikeExecPolicyError(cleaned))
            {
                cleaned +=
                    "\n[hint] PowerShell blocked the script due to an execution policy. " +
                    "BashTool passes -ExecutionPolicy Bypass, so this usually means a group " +
                    "policy is overriding the per-invocation setting. To allow scripts for " +
                    "the current user, run in an elevated prompt: " +
                    "Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned";
            }

            return cleaned;
        }
        catch (InvalidOperationException ex) { return $"Error starting process: {ex.Message}"; }
        catch (System.ComponentModel.Win32Exception ex) { return $"Error starting shell: {ex.Message}"; }
        catch (IOException ex) { return $"Error executing command: {ex.Message}"; }
    }

    /// <summary>
    /// Scans <paramref name="output"/> for the cwd sentinel line, removes it,
    /// and if present updates <see cref="_sessionState"/>. Returns the cleaned
    /// output suitable for showing to the model.
    /// </summary>
    private string ExtractCwdAndClean(string output)
    {
        if (string.IsNullOrEmpty(output)) return output;

        // The sentinel should appear on its own line. Use the last occurrence
        // in case the user command echoed the marker literally (unlikely).
        var idx = output.LastIndexOf(CwdSentinel, StringComparison.Ordinal);
        if (idx < 0) return output;

        // Walk forward to end-of-line to capture the cwd value.
        var eol = output.IndexOf('\n', idx);
        var endOfValue = eol < 0 ? output.Length : eol;
        var newCwd = output[(idx + CwdSentinel.Length)..endOfValue].TrimEnd('\r', ' ', '\t');

        // Rebuild output with the sentinel line removed. Drop the preceding
        // newline too so we don't leave a blank line behind.
        var lineStart = idx;
        if (lineStart > 0 && output[lineStart - 1] == '\n') lineStart--;
        if (lineStart > 0 && output[lineStart - 1] == '\r') lineStart--;
        var lineEnd = eol < 0 ? output.Length : eol + 1;
        var cleaned = output[..lineStart] + output[lineEnd..];

        if (!string.IsNullOrWhiteSpace(newCwd) && Directory.Exists(newCwd))
        {
            _sessionState.CurrentDirectory = newCwd;
        }

        return cleaned;
    }

    /// <summary>
    /// Recognizes the error text PowerShell prints when an execution policy
    /// blocks script execution. Matches both Windows PowerShell 5.1 and
    /// pwsh 7+ phrasing — both contain "running scripts is disabled".
    /// </summary>
    private static bool LooksLikeExecPolicyError(string output) =>
        !string.IsNullOrEmpty(output) &&
        (output.Contains("running scripts is disabled", StringComparison.OrdinalIgnoreCase)
         || output.Contains("UnauthorizedAccess", StringComparison.Ordinal)
         || output.Contains("cannot be loaded because", StringComparison.OrdinalIgnoreCase));

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
