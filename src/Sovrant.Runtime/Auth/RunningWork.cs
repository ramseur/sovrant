using System.Collections.Concurrent;

namespace Sovrant.Runtime.Auth;

/// <summary>
/// Phase 145 A8.3 — who is running what in this process (chat replies, workflow runs, swarms), so a
/// security sign-out can stop it: "Sign out everywhere", an admin revoke and disabling an account
/// stop that person's running work. Ordinary sign-out and idle timeout don't — work outlives those.
/// Process-local: it covers the Web or Server process that runs the work.
/// </summary>
public static class RunningWork
{
    public const string StoppedMessage =
        "Stopped because this account was signed out everywhere or disabled by an administrator.";

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<Ticket, byte>> s_byOwner = new(StringComparer.Ordinal);

    /// <summary>Records a run of <paramref name="ownerUserId"/>'s; dispose when it ends.</summary>
    public static Ticket Track(string? ownerUserId, CancellationTokenSource cts)
    {
        ArgumentNullException.ThrowIfNull(cts);
        var ticket = new Ticket(ownerUserId, cts);
        if (!string.IsNullOrEmpty(ownerUserId))
            s_byOwner.GetOrAdd(ownerUserId, _ => new()).TryAdd(ticket, 0);
        return ticket;
    }

    /// <summary>Stops every run of <paramref name="ownerUserId"/>'s. Returns how many were stopped.</summary>
    public static int StopAllFor(string ownerUserId)
    {
        if (string.IsNullOrEmpty(ownerUserId) || !s_byOwner.TryGetValue(ownerUserId, out var runs)) return 0;
        var stopped = 0;
        foreach (var ticket in runs.Keys)
        {
            if (ticket.Stop()) stopped++;
        }
        return stopped;
    }

    /// <summary>How many runs <paramref name="ownerUserId"/> has going.</summary>
    public static int CountFor(string ownerUserId) =>
        s_byOwner.TryGetValue(ownerUserId, out var runs) ? runs.Count : 0;

    public sealed class Ticket : IDisposable
    {
        private readonly string? _owner;
        private readonly CancellationTokenSource _cts;
        private int _stopped;

        internal Ticket(string? owner, CancellationTokenSource cts)
        {
            _owner = owner;
            _cts = cts;
        }

        /// <summary>True when this run was stopped by a security sign-out.</summary>
        public bool StoppedBySignOut => Volatile.Read(ref _stopped) == 1;

        internal bool Stop()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 1) return false;
            try { _cts.Cancel(); }
            catch (ObjectDisposedException) { return false; }
            return true;
        }

        public void Dispose()
        {
            if (_owner is not null && s_byOwner.TryGetValue(_owner, out var runs))
                runs.TryRemove(this, out _);
        }
    }
}
