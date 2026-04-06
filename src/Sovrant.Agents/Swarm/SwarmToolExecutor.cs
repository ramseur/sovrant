using System.Text.Json;
using Sovrant.Runtime.Tools;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Decorator around <see cref="IToolExecutor"/> that enforces file-level locking
/// during swarm execution. Write-oriented tools (<c>WriteFile</c>, <c>EditFile</c>,
/// <c>NotebookEdit</c>) are checked against the <see cref="SwarmFileLockManager"/>
/// before execution. If the target file is locked by another task the write is blocked.
/// If it's unlocked the executor auto-acquires a lock on behalf of the current task.
/// </summary>
internal sealed class SwarmToolExecutor : IToolExecutor
{
    /// <summary>Tools that write to a file identified by a JSON property.</summary>
    private static readonly Dictionary<string, string> s_writeTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WriteFile"] = "file_path",
        ["EditFile"] = "file_path",
        ["NotebookEdit"] = "notebook_path",
    };

    private readonly IToolExecutor _inner;
    private readonly SwarmFileLockManager _lockManager;
    private readonly string _taskId;

    public SwarmToolExecutor(IToolExecutor inner, SwarmFileLockManager lockManager, string taskId)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(lockManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        _inner = inner;
        _lockManager = lockManager;
        _taskId = taskId;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName, JsonElement input, CancellationToken ct = default)
    {
        // Check if this is a write tool with a known file-path property.
        if (s_writeTools.TryGetValue(toolName, out var pathProp))
        {
            var filePath = input.ValueKind == JsonValueKind.Object
                && input.TryGetProperty(pathProp, out var fp)
                    ? fp.GetString()
                    : null;

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                // If another task holds the lock, block.
                if (_lockManager.IsLockedByOther(filePath, _taskId))
                {
                    var holder = _lockManager.GetHolder(filePath) ?? "unknown";
                    return new ToolExecutionResult(
                        false,
                        $"Blocked: file '{filePath}' is locked by task '{holder}'. " +
                        "Declare the file in FilesToModify or wait for the other task to finish.",
                        IsError: true);
                }

                // Auto-acquire the lock for files the agent writes to but didn't declare.
                _lockManager.TryAcquire(filePath, _taskId);
            }
        }

        return await _inner.ExecuteAsync(toolName, input, ct).ConfigureAwait(false);
    }
}
