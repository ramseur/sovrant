using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Worktree;

/// <summary>
/// Creates a git worktree at the specified path and branch, then switches the session
/// working context to that worktree so subsequent file/shell operations target it.
/// </summary>
public sealed class EnterWorktreeTool : ITool
{
    private static readonly ToolDefinition s_definition = new("EnterWorktree", CreateSchema())
    {
        Description =
            "Creates a git worktree at a given path and branch, then switches the session " +
            "to that worktree. Subsequent file/shell operations should target the worktree path. " +
            "Use ExitWorktree to clean up and return to the main working tree.",
    };

    private readonly WorktreeState _state;

    public EnterWorktreeTool(WorktreeState state) => _state = state;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var path = GetString(input, "path");
        var branch = GetString(input, "branch");

        if (string.IsNullOrWhiteSpace(path))
            return "Error: path is required.";
        if (string.IsNullOrWhiteSpace(branch))
            return "Error: branch is required.";

        if (_state.IsActive)
            return $"Error: already in worktree '{_state.Path}'. Call ExitWorktree first.";

        // Run: git worktree add <path> <branch>
        // If branch does not exist, add -b to create it.
        var createNew = input.TryGetProperty("create_branch", out var cb) && cb.GetBoolean();
        var args = createNew
            ? $"worktree add -b {EscapeArg(branch)} {EscapeArg(path)}"
            : $"worktree add {EscapeArg(path)} {EscapeArg(branch)}";

        var (output, exitCode) = await RunGitAsync(args, ct).ConfigureAwait(false);
        if (exitCode != 0)
            return $"Error: git {args}\n{output}";

        _state.Path = System.IO.Path.GetFullPath(path);
        return $"Worktree created at '{_state.Path}' on branch '{branch}'.\n{output}".TrimEnd();
    }

    private static async Task<(string Output, int ExitCode)> RunGitAsync(string args, CancellationToken ct)
    {
        var sb = new StringBuilder();
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return (sb.ToString().Trim(), proc.ExitCode);
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static string EscapeArg(string arg) =>
        arg.Contains(' ', StringComparison.Ordinal) ? $"\"{arg}\"" : arg;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "path":          {"type": "string",  "description": "Filesystem path for the new worktree directory."},
                "branch":        {"type": "string",  "description": "Branch name to check out in the worktree."},
                "create_branch": {"type": "boolean", "description": "If true, create the branch (-b). Default false."}
            },
            "required": ["path", "branch"]
        }
        """).RootElement;
}
