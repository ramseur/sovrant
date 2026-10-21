using System.Collections.Concurrent;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 148 — workflow runs in progress in this process, so cancelling a workflow (Workflows page,
/// API, <c>/workflow cancel</c>) also stops its run instead of letting it carry on and write its own
/// status over the cancel. Process-local; the store's cancelled-stays-cancelled rule covers the rest.
/// </summary>
public static class WorkflowRuns
{
    private static readonly ConcurrentDictionary<string, Ticket> s_running = new(StringComparer.Ordinal);

    public static Ticket Track(string workflowId, CancellationTokenSource cts)
    {
        ArgumentNullException.ThrowIfNull(workflowId);
        ArgumentNullException.ThrowIfNull(cts);
        var ticket = new Ticket(workflowId, cts);
        s_running[workflowId] = ticket;
        return ticket;
    }

    /// <summary>Stops <paramref name="workflowId"/>'s run if one is going here. True when one was stopped.</summary>
    public static bool Stop(string workflowId) =>
        s_running.TryGetValue(workflowId, out var ticket) && ticket.Stop();

    public static bool IsRunning(string workflowId) => s_running.ContainsKey(workflowId);

    public sealed class Ticket : IDisposable
    {
        private readonly string _workflowId;
        private readonly CancellationTokenSource _cts;
        private int _stopped;

        internal Ticket(string workflowId, CancellationTokenSource cts)
        {
            _workflowId = workflowId;
            _cts = cts;
        }

        /// <summary>True when the run was stopped because the workflow was cancelled.</summary>
        public bool Cancelled => Volatile.Read(ref _stopped) == 1;

        internal bool Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 1) return false;
            try { _cts.Cancel(); }
            catch (ObjectDisposedException) { return false; }
            return true;
        }

        public void Dispose() => s_running.TryRemove(new KeyValuePair<string, Ticket>(_workflowId, this));
    }
}
