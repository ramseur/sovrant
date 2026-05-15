using System.Diagnostics;
using System.Security.Cryptography;

namespace Sovrant.Cli;

/// <summary>
/// Displays rotating "thinking" phrases in teal while the model is processing.
/// Uses manual console writes to avoid Spectre.Console live-context conflicts.
/// </summary>
internal sealed class ThinkingSpinner : IDisposable
{
    private static readonly string[] s_phrases =
    [
        "Thinking really hard...",
        "Consulting the oracle...",
        "Pondering the possibilities...",
        "Gathering thoughts...",
        "Connecting the dots...",
        "Reasoning through it...",
        "Weighing the options...",
        "Mulling it over...",
    ];

    private static readonly int s_maxLen = s_phrases.Max(p => p.Length) + 16; // extra room for elapsed

    private readonly CancellationTokenSource _cts = new();
    private readonly Stopwatch _stopwatch = new();
    private Task? _spinnerTask;

    /// <summary>Total elapsed time from start to stop.</summary>
    internal TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>Starts the spinner on a background thread.</summary>
    internal void Start()
    {
        _stopwatch.Restart();
        _spinnerTask = Task.Run(async () =>
        {
            var idx = RandomNumberGenerator.GetInt32(s_phrases.Length);
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var phrase = s_phrases[idx % s_phrases.Length];
                    var elapsed = FormatElapsed(_stopwatch.Elapsed);
                    var line = $"{phrase}  \x1b[38;5;243m{elapsed}\x1b[0m";
                    // Teal ANSI color (38;5;6) + phrase + dim elapsed + reset + pad + carriage return
                    Console.Write($"\r  \x1b[38;5;6m{line}\x1b[0m{"".PadRight(Math.Max(0, s_maxLen - phrase.Length - elapsed.Length))}");
                    idx++;
                    await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on stop.
            }
            finally
            {
                // Clear the spinner line.
                Console.Write($"\r{"".PadRight(s_maxLen + 4)}\r");
            }
        });
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes >= 1)
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        return $"{(int)elapsed.TotalSeconds}s";
    }

    /// <summary>Stops the spinner and clears the status line.</summary>
    internal async Task StopAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_spinnerTask is not null)
            await _spinnerTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    public void Dispose() => _cts.Dispose();
}
