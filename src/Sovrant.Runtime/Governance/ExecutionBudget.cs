namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59c — per-plan resource limits that prevent runaway execution.
/// The executor checks the budget before each tool call and pauses
/// execution (notifying the user) if any limit is exceeded.
/// </summary>
public sealed record ExecutionBudgetConfig(
    int MaxToolCalls = 50,
    int MaxFilesModified = 20,
    TimeSpan? MaxExecutionTime = null)
{
    /// <summary>Default budget suitable for most interactive sessions.</summary>
    public static readonly ExecutionBudgetConfig Default = new();
}

/// <summary>Current state of budget consumption.</summary>
public enum ExecutionBudgetStatus
{
    /// <summary>Budget is within limits.</summary>
    WithinBudget,
    /// <summary>Budget has been exceeded.</summary>
    Exceeded,
}

/// <summary>
/// Tracks resource consumption against the configured budget limits.
/// Thread-safe for parallel step execution.
/// </summary>
public sealed class ExecutionBudget
{
    private readonly ExecutionBudgetConfig _config;
    private readonly DateTimeOffset _startedAt;
    private readonly HashSet<string> _modifiedFiles = new(StringComparer.OrdinalIgnoreCase);
    private int _toolCallCount;

    public ExecutionBudget(ExecutionBudgetConfig? config = null)
    {
        _config = config ?? ExecutionBudgetConfig.Default;
        _startedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Current budget status.</summary>
    public ExecutionBudgetStatus Status
    {
        get
        {
            if (_toolCallCount > _config.MaxToolCalls)
                return ExecutionBudgetStatus.Exceeded;
            if (_modifiedFiles.Count > _config.MaxFilesModified)
                return ExecutionBudgetStatus.Exceeded;
            if (_config.MaxExecutionTime is { } max && DateTimeOffset.UtcNow - _startedAt > max)
                return ExecutionBudgetStatus.Exceeded;
            return ExecutionBudgetStatus.WithinBudget;
        }
    }

    /// <summary>Number of tool calls consumed.</summary>
    public int ToolCallCount => _toolCallCount;

    /// <summary>Number of unique files modified.</summary>
    public int FilesModified => _modifiedFiles.Count;

    /// <summary>
    /// Attempts to consume one tool call from the budget. Returns
    /// <see langword="false"/> if the budget is already exceeded.
    /// </summary>
    /// <param name="toolName">The tool being invoked.</param>
    /// <param name="filePath">Optional file path if the tool modifies a file.</param>
    public bool TryConsume(string toolName, string? filePath = null)
    {
        if (Status == ExecutionBudgetStatus.Exceeded)
            return false;

        Interlocked.Increment(ref _toolCallCount);
        if (_toolCallCount > _config.MaxToolCalls)
            return false;

        if (filePath is not null && GraduatedToolTiers.IsAtLeast(toolName, ToolTier.Moderate))
        {
            lock (_modifiedFiles)
            {
                // Tentatively add; if it would exceed budget, remove and fail.
                if (_modifiedFiles.Add(filePath) && _modifiedFiles.Count > _config.MaxFilesModified)
                {
                    _modifiedFiles.Remove(filePath);
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Returns a human-readable summary of remaining budget.</summary>
    public string Summarize() =>
        $"Tool calls: {_toolCallCount}/{_config.MaxToolCalls}, " +
        $"Files modified: {_modifiedFiles.Count}/{_config.MaxFilesModified}" +
        (_config.MaxExecutionTime is { } max
            ? $", Time: {DateTimeOffset.UtcNow - _startedAt:mm\\:ss}/{max:mm\\:ss}"
            : "");
}
