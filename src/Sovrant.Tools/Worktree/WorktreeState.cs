namespace Sovrant.Tools.Worktree;

/// <summary>Tracks the active git worktree path for the current session.</summary>
public sealed class WorktreeState
{
    private volatile string? _path;

    /// <summary>
    /// The filesystem path of the currently active worktree,
    /// or <see langword="null"/> if no worktree is active.
    /// </summary>
    public string? Path
    {
        get => _path;
        set => _path = value;
    }

    /// <summary>Returns <see langword="true"/> if a worktree is currently active.</summary>
    public bool IsActive => _path is not null;
}
