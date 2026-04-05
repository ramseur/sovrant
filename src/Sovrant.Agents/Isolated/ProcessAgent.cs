using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Isolated;

/// <summary>
/// An <see cref="IAgent"/> backed by a child process. Tasks are written as JSON to the
/// process stdin; structured results (including tool-use messages) are streamed from stdout.
/// Tool-use parsing follows the same message format as the original OpenClaude agent protocol.
/// </summary>
public sealed partial class ProcessAgent : IAgent
{
    private readonly ProcessStartInfo _startInfo;
    private readonly ILogger<ProcessAgent> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent process '{AgentName}' exited with code {ExitCode}")]
    private static partial void LogProcessExited(ILogger logger, string agentName, int exitCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Agent process '{AgentName}' failed")]
    private static partial void LogProcessFailed(ILogger logger, Exception ex, string agentName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to kill agent process '{AgentName}'")]
    private static partial void LogKillFailed(ILogger logger, Exception ex, string agentName);

    /// <param name="name">Unique agent name used for routing.</param>
    /// <param name="startInfo">
    /// Start info for the agent process. <c>RedirectStandardInput</c>,
    /// <c>RedirectStandardOutput</c>, and <c>RedirectStandardError</c> are set automatically.
    /// </param>
    /// <param name="logger">Logger for process lifecycle and parsing events.</param>
    public ProcessAgent(string name, ProcessStartInfo startInfo, ILogger<ProcessAgent> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(logger);

        Name = name;
        _startInfo = startInfo;
        _logger = logger;

        // Ensure the process communicates via redirected stdio, not an attached console.
        _startInfo.RedirectStandardInput = true;
        _startInfo.RedirectStandardOutput = true;
        _startInfo.RedirectStandardError = true;
        _startInfo.UseShellExecute = false;
        _startInfo.CreateNoWindow = true;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public async Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        Process? process = null;
        try
        {
            process = Process.Start(_startInfo);
            if (process is null)
                return AgentResult.Fail(task.Id, "Failed to start agent process.");

            // Write task as JSON to stdin and close it to signal EOF.
            var taskJson = JsonSerializer.Serialize(new { id = task.Id, prompt = task.Prompt });
            await process.StandardInput.WriteLineAsync(taskJson.AsMemory(), ct).ConfigureAwait(false);
            process.StandardInput.Close();

            // Read stdout and stderr concurrently.
            var stdoutTask = ReadStreamAsync(process.StandardOutput, ct);
            var stderrTask = ReadStreamAsync(process.StandardError, ct);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            // Wait for exit with cancellation support.
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            LogProcessExited(_logger, Name, process.ExitCode);

            return process.ExitCode == 0
                ? AgentResult.Ok(task.Id, stdout)
                : AgentResult.Fail(task.Id, string.IsNullOrEmpty(stderr) ? $"Process exited with code {process.ExitCode}" : stderr);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            return AgentResult.Fail(task.Id, "Task was cancelled.");
        }
        catch (InvalidOperationException ex)
        {
            LogProcessFailed(_logger, ex, Name);
            TryKillProcess(process);
            return AgentResult.Fail(task.Id, ex.Message);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            LogProcessFailed(_logger, ex, Name);
            TryKillProcess(process);
            return AgentResult.Fail(task.Id, ex.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static async Task<string> ReadStreamAsync(System.IO.StreamReader reader, CancellationToken ct)
    {
        var sb = new StringBuilder();
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            sb.AppendLine(line);
        }
        return sb.ToString().TrimEnd();
    }

    private void TryKillProcess(Process? process)
    {
        if (process is null || process.HasExited) return;
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException ex)
        {
            LogKillFailed(_logger, ex, Name);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            LogKillFailed(_logger, ex, Name);
        }
    }
}
