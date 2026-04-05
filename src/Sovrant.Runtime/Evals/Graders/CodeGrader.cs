using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sovrant.Runtime.Evals.Graders;

/// <summary>
/// Deterministic grader: runs a command and checks exit code and/or output pattern.
/// <c>{output}</c> in the command is replaced with the eval output (via a temp file for safety).
/// </summary>
internal sealed class CodeGrader : IGrader
{
    private const int DefaultTimeoutMs = 120_000; // 2 minutes
    private const int OutputCapChars = 64 * 1024;

    public async Task<GradeResult> GradeAsync(EvalDefinition eval, string output, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var grader = eval.Grader;

        // If a command is configured, run it
        if (!string.IsNullOrWhiteSpace(grader.Command))
        {
            var commandResult = await RunCommandAsync(grader.Command, output, ct).ConfigureAwait(false);
            sw.Stop();

            // Check exit code first
            if (commandResult.ExitCode != 0)
                return new GradeResult(false, commandResult.Output, Duration: sw.Elapsed);

            // If a pattern is also specified, check it against the command output
            if (!string.IsNullOrWhiteSpace(grader.Pattern))
            {
                var matches = Regex.IsMatch(commandResult.Output, grader.Pattern, RegexOptions.None, TimeSpan.FromSeconds(5));
                return new GradeResult(matches, commandResult.Output, Duration: sw.Elapsed);
            }

            return new GradeResult(true, commandResult.Output, Duration: sw.Elapsed);
        }

        // No command — just pattern-match the raw eval output
        if (!string.IsNullOrWhiteSpace(grader.Pattern))
        {
            var matches = Regex.IsMatch(output, grader.Pattern, RegexOptions.None, TimeSpan.FromSeconds(5));
            sw.Stop();
            return new GradeResult(matches, matches ? "Pattern matched." : "Pattern did not match.", Duration: sw.Elapsed);
        }

        // Neither command nor pattern configured — always passes (misconfiguration, but lenient)
        sw.Stop();
        return new GradeResult(true, "No command or pattern configured — auto-pass.", Duration: sw.Elapsed);
    }

    private static async Task<(string Output, int ExitCode)> RunCommandAsync(
        string command, string evalOutput, CancellationToken ct)
    {
        // Write eval output to a temp file so {output} can reference it safely
        var tempFile = Path.Combine(Path.GetTempPath(), $"sovrant-eval-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(tempFile, evalOutput, ct).ConfigureAwait(false);
            var expandedCommand = command.Replace("{output}", tempFile, StringComparison.OrdinalIgnoreCase);

            var shellName = OperatingSystem.IsWindows() ? "cmd" : "sh";
            var shellArgs = OperatingSystem.IsWindows()
                ? $"/c {expandedCommand}"
                : $"-c {expandedCommand}";

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(DefaultTimeoutMs);

            var stdoutSb = new StringBuilder();
            var stderrSb = new StringBuilder();

            var psi = new ProcessStartInfo
            {
                FileName = shellName,
                Arguments = shellArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null && stdoutSb.Length < OutputCapChars)
                    stdoutSb.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null && stderrSb.Length < OutputCapChars)
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
                return (string.Create(CultureInfo.InvariantCulture,
                    $"Error: grader command timed out after {DefaultTimeoutMs}ms."), -1);
            }

            var result = new StringBuilder();
            if (stdoutSb.Length > 0) result.Append(stdoutSb);
            if (stderrSb.Length > 0) result.Append(CultureInfo.InvariantCulture, $"[stderr]\n{stderrSb}");

            return (result.Length > 0 ? result.ToString() : string.Create(CultureInfo.InvariantCulture,
                $"[exit code: {process.ExitCode}]"), process.ExitCode);
        }
        finally
        {
            try { File.Delete(tempFile); } catch (IOException) { /* best-effort cleanup */ }
        }
    }
}
