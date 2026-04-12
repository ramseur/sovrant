using System.Collections.Concurrent;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Pessimistic file-level locking for swarm execution.
/// Prevents multiple agents from modifying the same file concurrently.
/// </summary>
public sealed class SwarmFileLockManager : ISwarmFileLockManager
{
    private readonly ConcurrentDictionary<string, string> _fileLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Attempts to acquire a lock on <paramref name="filePath"/> for <paramref name="taskId"/>.</summary>
    /// <returns><c>true</c> if the lock was acquired or already held by the same task.</returns>
    public bool TryAcquire(string filePath, string taskId)
    {
        var normalized = NormalizePath(filePath);
        return _fileLocks.TryAdd(normalized, taskId) ||
               (_fileLocks.TryGetValue(normalized, out var holder) && holder == taskId);
    }

    /// <summary>Returns <c>true</c> if the file is locked by a task other than <paramref name="taskId"/>.</summary>
    public bool IsLockedByOther(string filePath, string taskId)
    {
        var normalized = NormalizePath(filePath);
        return _fileLocks.TryGetValue(normalized, out var holder) && holder != taskId;
    }

    /// <summary>Returns the task ID holding the lock, or <c>null</c> if unlocked.</summary>
    public string? GetHolder(string filePath)
    {
        var normalized = NormalizePath(filePath);
        return _fileLocks.TryGetValue(normalized, out var holder) ? holder : null;
    }

    /// <summary>Releases all locks held by <paramref name="taskId"/>.</summary>
    public void ReleaseAll(string taskId)
    {
        var keysToRemove = _fileLocks
            .Where(kvp => kvp.Value == taskId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
            _fileLocks.TryRemove(key, out _);
    }

    /// <summary>Releases all locks (used when swarm completes).</summary>
    public void Clear() => _fileLocks.Clear();

    /// <summary>Returns a snapshot of all current locks.</summary>
    public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_fileLocks);

    private static string NormalizePath(string path) => Path.GetFullPath(path).Replace('\\', '/');
}
