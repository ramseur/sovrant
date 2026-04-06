using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Sovrant.Tools;

/// <summary>
/// Shared helper for spawning a process, capturing stdout/stderr, and handling
/// timeout. Reduces duplication across BashTool, PowerShellTool, ReplTool, etc.
/// </summary>
internal static class ProcessExecutor
{
    /// <summary>Result of a process execution.</summary>
    internal readonly record struct Result(string Output, int ExitCode);

    /// <summary>
    /// Runs a process to completion, capturing stdout and stderr.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">Arguments to pass (added via <see cref="ProcessStartInfo.ArgumentList"/>).</param>
    /// <param name="timeoutMs">Maximum runtime in milliseconds.</param>
    /// <param name="outputCapChars">Maximum characters to capture per stream (0 = unlimited).</param>
    /// <param name="ct">Caller cancellation token.</param>
    internal static async Task<Result> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        int timeoutMs,
        int outputCapChars = 0,
        CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        var stdoutSb = new StringBuilder();
        var stderrSb = new StringBuilder();
        var stdoutTruncated = false;
        var stderrTruncated = false;

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            if (outputCapChars > 0 && stdoutSb.Length >= outputCapChars)
                stdoutTruncated = true;
            else
                stdoutSb.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            if (outputCapChars > 0 && stderrSb.Length >= outputCapChars)
                stderrTruncated = true;
            else
                stderrSb.AppendLine(e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new Result($"Error: command timed out after {timeoutMs} ms.", -1);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new Result($"Error: failed to start '{fileName}': {ex.Message}", -1);
        }

        var stdout = stdoutSb.ToString();
        if (stdoutTruncated) stdout += $"\n[stdout truncated at {outputCapChars / 1024} KB]";
        var stderr = stderrSb.ToString();
        if (stderrTruncated) stderr += $"\n[stderr truncated at {outputCapChars / 1024} KB]";

        var result = new StringBuilder();
        if (!string.IsNullOrEmpty(stdout)) result.Append(stdout);
        if (!string.IsNullOrEmpty(stderr)) result.Append(CultureInfo.InvariantCulture, $"[stderr]\n{stderr}");
        if (process.ExitCode != 0) result.Append(CultureInfo.InvariantCulture, $"\n[exit code: {process.ExitCode}]");

        var output = result.Length > 0
            ? result.ToString()
            : string.Create(CultureInfo.InvariantCulture, $"[exit code: {process.ExitCode}]");

        return new Result(output, process.ExitCode);
    }

    /// <summary>
    /// Writes <paramref name="content"/> to a temp file, runs <paramref name="fileName"/>
    /// with the file as an argument, then deletes the temp file.
    /// </summary>
    internal static async Task<Result> RunWithTempFileAsync(
        string fileName,
        IReadOnlyList<string> prefixArgs,
        string content,
        string tempFileExtension,
        int timeoutMs,
        int outputCapChars = 0,
        CancellationToken ct = default)
    {
        var scriptFile = Path.Combine(Path.GetTempPath(), $"sovrant_{Guid.NewGuid():N}{tempFileExtension}");
        try
        {
            await File.WriteAllTextAsync(scriptFile, content, ct).ConfigureAwait(false);
            var args = new List<string>(prefixArgs) { scriptFile };
            return await RunAsync(fileName, args, timeoutMs, outputCapChars, ct).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(scriptFile); } catch (IOException) { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Removes dangerous environment variables that could be used for library injection.
    /// </summary>
    internal static void RemoveDangerousEnvVars(ProcessStartInfo psi)
    {
        string[] dangerous = ["LD_PRELOAD", "DYLD_INSERT_LIBRARIES", "DYLD_LIBRARY_PATH",
            "LD_LIBRARY_PATH", "LD_AUDIT", "LD_DEBUG"];
        foreach (var envVar in dangerous)
            psi.EnvironmentVariables.Remove(envVar);
    }
}
