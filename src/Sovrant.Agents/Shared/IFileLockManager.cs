namespace Sovrant.Agents.Shared;

/// <summary>
/// Pessimistic file-level locking for concurrent agent execution.
/// Shared between Swarm and Team run paths so parallel members can't stomp each other's writes.
/// </summary>
public interface IFileLockManager
{
    /// <summary>Attempts to acquire a lock on <paramref name="filePath"/> for <paramref name="taskId"/>.</summary>
    bool TryAcquire(string filePath, string taskId);

    /// <summary>Returns <c>true</c> if the file is locked by a task other than <paramref name="taskId"/>.</summary>
    bool IsLockedByOther(string filePath, string taskId);

    /// <summary>Returns the task ID holding the lock, or <c>null</c> if unlocked.</summary>
    string? GetHolder(string filePath);

    /// <summary>Releases all locks held by <paramref name="taskId"/>.</summary>
    void ReleaseAll(string taskId);

    /// <summary>Releases all locks (used when a run completes).</summary>
    void Clear();

    /// <summary>Returns a snapshot of all current locks.</summary>
    IReadOnlyDictionary<string, string> Snapshot();
}
