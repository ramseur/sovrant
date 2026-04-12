using System.Collections.Concurrent;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>A single audit log entry from the ethical harness.</summary>
public sealed record EthicalAuditEntry(
    DateTimeOffset Timestamp,
    string Category,
    string Reason,
    EthicalSeverity Severity,
    bool IsOutbound);

/// <summary>
/// Thread-safe in-memory audit log for ethical harness decisions.
/// Records every block and flag for compliance reporting.
/// </summary>
public sealed class EthicalAuditLog
{
    private readonly ConcurrentQueue<EthicalAuditEntry> _entries = new();
    private readonly int _maxEntries;

    public EthicalAuditLog(int maxEntries = 10_000)
    {
        _maxEntries = maxEntries;
    }

    /// <summary>Records an audit entry.</summary>
    public void Record(string category, string reason, EthicalSeverity severity, bool isOutbound)
    {
        _entries.Enqueue(new EthicalAuditEntry(DateTimeOffset.UtcNow, category, reason, severity, isOutbound));

        // Evict oldest entries if over capacity.
        while (_entries.Count > _maxEntries)
            _entries.TryDequeue(out _);
    }

    /// <summary>Returns all recorded entries (newest last).</summary>
    public IReadOnlyList<EthicalAuditEntry> Entries => [.. _entries];

    /// <summary>Number of recorded entries.</summary>
    public int Count => _entries.Count;

    /// <summary>Clears all entries.</summary>
    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}
