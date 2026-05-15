using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Tasks;

/// <summary>Spawns a shell command as a background task and returns its task ID.</summary>
public sealed class TaskCreateTool : ITool
{
    private const int DefaultTimeoutMs = 300_000; // 5 minutes

    private static readonly ToolDefinition s_definition = new("TaskCreate", CreateSchema())
    {
        Description =
            "Runs a shell command as a background task. Returns a task ID that can be used " +
            "with TaskGet, TaskOutput, and TaskStop. The task runs asynchronously.",
    };

    private readonly BackgroundTaskRegistry _registry;

    public TaskCreateTool(BackgroundTaskRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var command = input.GetStringProp("command");
        if (string.IsNullOrWhiteSpace(command))
            return Task.FromResult("Error: command is required.");

        var description = input.GetStringProp("description", command[..Math.Min(command.Length, 50)]);
        var timeoutMs = input.GetIntProp("timeout", DefaultTimeoutMs);

        var info = new BackgroundTaskInfo
        {
            Command = command,
            Description = description,
        };
        _registry.Add(info);

        // Fire and forget — run the process in background
        _ = RunProcessAsync(info, command, timeoutMs);

        return Task.FromResult(string.Create(CultureInfo.InvariantCulture, $"Task started. ID: {info.Id}\nCommand: {command}"));
    }

    private static async Task RunProcessAsync(BackgroundTaskInfo info, string command, int timeoutMs)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(info.Cts.Token);
        cts.CancelAfter(timeoutMs);

        // Write command to a temp file to avoid shell injection via argument escaping.
        string shell;
        string scriptFile;
        if (OperatingSystem.IsWindows())
        {
            var pwsh = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell", "7", "pwsh.exe");
            if (File.Exists(pwsh))
            {
                shell = pwsh;
                scriptFile = Path.Combine(Path.GetTempPath(), $"sovrant_task_{Guid.NewGuid():N}.ps1");
            }
            else
            {
                shell = "cmd.exe";
                scriptFile = Path.Combine(Path.GetTempPath(), $"sovrant_task_{Guid.NewGuid():N}.cmd");
            }
        }
        else
        {
            shell = "/bin/bash";
            scriptFile = Path.Combine(Path.GetTempPath(), $"sovrant_task_{Guid.NewGuid():N}.sh");
        }

        try
        {
            await File.WriteAllTextAsync(scriptFile, command, cts.Token).ConfigureAwait(false);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            if (shell == "cmd.exe")
            {
                process.StartInfo.ArgumentList.Add("/c");
            }
            else if (shell.EndsWith("pwsh.exe", StringComparison.OrdinalIgnoreCase))
            {
                process.StartInfo.ArgumentList.Add("-NonInteractive");
                process.StartInfo.ArgumentList.Add("-File");
            }
            process.StartInfo.ArgumentList.Add(scriptFile);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    lock (info.OutputBuffer) { info.OutputBuffer.AppendLine(e.Data); }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    lock (info.OutputBuffer) { info.OutputBuffer.AppendLine(CultureInfo.InvariantCulture, $"[stderr] {e.Data}"); }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            info.ExitCode = process.ExitCode;
            info.Status = process.ExitCode == 0 ? BackgroundTaskStatus.Completed : BackgroundTaskStatus.Failed;
        }
        catch (OperationCanceledException)
        {
            info.Status = BackgroundTaskStatus.Cancelled;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            lock (info.OutputBuffer) { info.OutputBuffer.AppendLine(CultureInfo.InvariantCulture, $"[error] {ex.Message}"); }
            info.Status = BackgroundTaskStatus.Failed;
        }
        finally
        {
            info.CompletedAt = DateTimeOffset.UtcNow;
            try { File.Delete(scriptFile); } catch (IOException) { /* best-effort cleanup */ }
        }
    }



    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "command":     {"type": "string",  "description": "Shell command to run in the background."},
                "description": {"type": "string",  "description": "Human-readable description."},
                "timeout":     {"type": "integer", "description": "Max runtime in ms (default 300000)."}
            },
            "required": ["command"]
        }
        """).RootElement;
}
