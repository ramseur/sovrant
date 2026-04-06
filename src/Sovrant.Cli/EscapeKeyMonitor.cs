namespace Sovrant.Cli;

/// <summary>
/// Monitors for Escape key presses in the background and cancels the provided
/// <see cref="CancellationTokenSource"/> when detected.
/// </summary>
internal sealed class EscapeKeyMonitor : IDisposable
{
    private readonly CancellationTokenSource _ctsToCancel;
    private readonly CancellationTokenSource _stopMonitor = new();
    private Task? _monitorTask;

    internal EscapeKeyMonitor(CancellationTokenSource ctsToCancel) =>
        _ctsToCancel = ctsToCancel;

    internal void Start()
    {
        _monitorTask = Task.Run(async () =>
        {
            try
            {
                while (!_stopMonitor.Token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        if (key.Key == ConsoleKey.Escape)
                        {
                            await _ctsToCancel.CancelAsync().ConfigureAwait(false);
                            return;
                        }
                    }

                    await Task.Delay(50, _stopMonitor.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on stop.
            }
        });
    }

    internal async Task StopAsync()
    {
        await _stopMonitor.CancelAsync().ConfigureAwait(false);
        if (_monitorTask is not null)
            await _monitorTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    public void Dispose() => _stopMonitor.Dispose();
}
