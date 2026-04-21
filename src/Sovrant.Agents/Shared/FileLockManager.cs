using System.Collections.Concurrent;

namespace Sovrant.Agents.Shared;

/// <summary>
/// Pessimistic file-level locking for concurrent agent execution.
/// Prevents multiple agents from modifying the same file at the same time.
/// </summary>
public sealed class FileLockManager : IFileLockManager
{
    private readonly ConcurrentDictionary<string, string> _fileLocks = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAcquire(string filePath, string taskId)
    {
        var normalized = NormalizePath(filePath);
        return _fileLocks.TryAdd(normalized, taskId) ||
               (_fileLocks.TryGetValue(normalized, out var holder) && holder == taskId);
    }

    public bool IsLockedByOther(string filePath, string taskId)
    {
        var normalized = NormalizePath(filePath);
        return _fileLocks.TryGetValue(normalized, out var holder) && holder != taskId;
    }

    public string? GetHolder(string filePath)
    {
        var normalized = NormalizePath(filePath);
        return _fileLocks.TryGetValue(normalized, out var holder) ? holder : null;
    }

    public void ReleaseAll(string taskId)
    {
        var keysToRemove = _fileLocks
            .Where(kvp => kvp.Value == taskId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
            _fileLocks.TryRemove(key, out _);
    }

    public void Clear() => _fileLocks.Clear();

    public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_fileLocks);

    private static string NormalizePath(string path) => Path.GetFullPath(path).Replace('\\', '/');
}
