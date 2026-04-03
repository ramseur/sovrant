using System.Collections.Concurrent;

namespace Sovrant.Tools.Tasks;

/// <summary>Tracks all background shell tasks spawned during the session.</summary>
public sealed class BackgroundTaskRegistry
{
    private readonly ConcurrentDictionary<string, BackgroundTaskInfo> _tasks = new(StringComparer.Ordinal);

    public BackgroundTaskInfo Add(BackgroundTaskInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _tasks[info.Id] = info;
        return info;
    }

    public bool TryGet(string id, out BackgroundTaskInfo? info) => _tasks.TryGetValue(id, out info);

    public IReadOnlyList<BackgroundTaskInfo> All => [.. _tasks.Values];
}

/// <summary>Information about a background shell task.</summary>
public sealed class BackgroundTaskInfo
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Command { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Running;
    public System.Text.StringBuilder OutputBuffer { get; } = new();
    public CancellationTokenSource Cts { get; } = new();
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int? ExitCode { get; set; }
}

/// <summary>The lifecycle status of a background task.</summary>
public enum BackgroundTaskStatus
{
    /// <summary>Task is currently running.</summary>
    Running,
    /// <summary>Task completed successfully (exit code 0).</summary>
    Completed,
    /// <summary>Task exited with a non-zero exit code.</summary>
    Failed,
    /// <summary>Task was cancelled.</summary>
    Cancelled,
}
