using System.Text.Json;
using Spectre.Console;
using Sovrant.Runtime.Permissions;
using Sovrant.Tools.Shell;

namespace Sovrant.Cli;

/// <summary>
/// Prompts the user interactively to approve or deny a tool invocation.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance", "CA1812", Justification = "Instantiated via DI.")]
internal sealed class InteractiveConfirmationHandler : IToolConfirmationHandler
{
    // Spectre.Console does not support concurrent interactive prompts.
    // Serialize confirmation requests so swarm / parallel agents queue up.
    private static readonly SemaphoreSlim s_gate = new(1, 1);

    private readonly ShellEnvironment _shellEnv;

    public InteractiveConfirmationHandler(ShellEnvironment shellEnv)
    {
        _shellEnv = shellEnv ?? throw new ArgumentNullException(nameof(shellEnv));
    }

    public async Task<bool> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
    {
        if (Console.IsInputRedirected)
            return false;

        await s_gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Show tool name and a summary of the input.
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"  [yellow]\u26a0 {Markup.Escape(toolName)}[/] requires confirmation:");

            // Phase 43 #21 — when the Sovrant process is running elevated
            // (Administrator on Windows, root on Unix), shell commands
            // inherit that elevation. Surface this on the confirmation so
            // the user isn't approving a `Remove-Item` without realizing
            // it'll bypass ACL prompts.
            if (_shellEnv.IsElevated && IsShellTool(toolName))
            {
                var role = OperatingSystem.IsWindows() ? "Administrator" : "root";
                AnsiConsole.MarkupLine(
                    $"    [red bold]\u26a0 running as {role} — this command will have elevated privileges.[/]");
            }

            // Show a compact summary of the input parameters.
            if (input.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in input.EnumerateObject())
                {
                    var val = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.GetRawText();
                    if (val.Length > 120)
                        val = string.Concat(val.AsSpan(0, 117), "...");
                    AnsiConsole.MarkupLine($"    [grey]{Markup.Escape(prop.Name)}:[/] {Markup.Escape(val)}");
                }
            }

            AnsiConsole.WriteLine();
            var confirmed = await AnsiConsole.ConfirmAsync("  Allow?", defaultValue: true, ct).ConfigureAwait(false);
            return confirmed;
        }
        finally
        {
            s_gate.Release();
        }
    }

    /// <summary>
    /// Tools that shell out and therefore carry the process's elevation.
    /// Used to decide whether to show the "running as admin" warning.
    /// </summary>
    private static bool IsShellTool(string toolName) =>
        toolName is "Bash" or "PowerShell" or "Repl" or "Verify";
}
