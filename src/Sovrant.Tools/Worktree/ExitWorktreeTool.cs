using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Worktree;

/// <summary>
/// Removes the active git worktree and clears the session worktree state,
/// returning subsequent operations to the main working tree.
/// </summary>
public sealed class ExitWorktreeTool : ITool
{
    private static readonly ToolDefinition s_definition = new("ExitWorktree", CreateSchema())
    {
        Description =
            "Removes the active git worktree and returns the session to the main working tree. " +
            "Pass force: true to remove even if there are uncommitted changes.",
    };

    private readonly WorktreeState _state;

    public ExitWorktreeTool(WorktreeState state) => _state = state;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var path = _state.Path;
        if (path is null)
            return "No active worktree. Nothing to exit.";

        var force = input.TryGetProperty("force", out var fv) && fv.GetBoolean();
        var gitArgs = force
            ? new[] { "worktree", "remove", "--force", path }
            : new[] { "worktree", "remove", path };

        var (output, exitCode) = await RunGitAsync(gitArgs, ct).ConfigureAwait(false);
        if (exitCode != 0)
            return $"Error removing worktree:\n{output}";

        _state.Path = null;
        var result = $"Worktree at '{path}' removed. Session returned to main working tree.";
        return string.IsNullOrEmpty(output) ? result : $"{result}\n{output}";
    }

    private static async Task<(string Output, int ExitCode)> RunGitAsync(string[] args, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return (sb.ToString().Trim(), proc.ExitCode);
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "force": {"type": "boolean", "description": "Force removal even with uncommitted changes. Default false."}
            }
        }
        """).RootElement;
}
