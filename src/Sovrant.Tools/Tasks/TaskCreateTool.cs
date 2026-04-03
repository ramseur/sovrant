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
        var command = GetString(input, "command");
        if (string.IsNullOrWhiteSpace(command))
            return Task.FromResult("Error: command is required.");

        var description = GetString(input, "description", command[..Math.Min(command.Length, 50)]);
        var timeoutMs = GetInt(input, "timeout", DefaultTimeoutMs);

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

        var shell = OperatingSystem.IsWindows() ? "bash.exe" : "/bin/bash";
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = $"-c \"{command.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

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
        catch (InvalidOperationException ex)
        {
            lock (info.OutputBuffer) { info.OutputBuffer.AppendLine(CultureInfo.InvariantCulture, $"[error] {ex.Message}"); }
            info.Status = BackgroundTaskStatus.Failed;
        }
        finally
        {
            info.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static int GetInt(JsonElement el, string prop, int def) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : def;

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
