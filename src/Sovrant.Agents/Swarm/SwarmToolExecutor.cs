using System.Text.Json;
using Sovrant.Agents.Shared;
using Sovrant.Runtime.Tools;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Decorator around <see cref="IToolExecutor"/> that enforces file-level locking
/// during swarm execution. Write-oriented tools (<c>Write</c>, <c>Edit</c>,
/// <c>NotebookEdit</c>) are checked against the <see cref="IFileLockManager"/>
/// before execution. If the target file is locked by another task the write is blocked.
/// If it's unlocked the executor auto-acquires a lock on behalf of the current task.
/// Locking is skipped entirely when the swarm runs with file locks disabled.
/// <para>
/// When <c>bypassConfirmation</c> is <see langword="true"/>, write tools execute
/// directly via the tool registry without prompting the user for confirmation.
/// </para>
/// </summary>
internal sealed class SwarmToolExecutor : IToolExecutor
{
    /// <summary>
    /// Tools that write to a file identified by a JSON property. Keys must match the
    /// registered tool names (<c>ToolDefinition.Name</c>) — this map previously used
    /// <c>WriteFile</c>/<c>EditFile</c>, which no tool is called, so locking never ran.
    /// </summary>
    /// <remarks>
    /// <c>RestrictToWorkingDirectory</c> keeps the working-directory guard where it
    /// has historically applied (NotebookEdit only). Write/Edit legitimately target
    /// project folders outside the process's current directory on Desktop/Web/Server;
    /// a real directory boundary for file tools is Phase 124's job.
    /// </remarks>
    private static readonly Dictionary<string, (string PathProperty, bool RestrictToWorkingDirectory)> s_writeTools =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Write"] = ("file_path", false),
            ["Edit"] = ("file_path", false),
            ["NotebookEdit"] = ("notebook_path", true),
        };

    private readonly IToolExecutor _inner;
    private readonly IFileLockManager _lockManager;
    private readonly string _taskId;
    private readonly bool _bypassConfirmation;
    private readonly IToolRegistry? _registry;
    private readonly bool _fileLocksEnabled;

    public SwarmToolExecutor(
        IToolExecutor inner,
        IFileLockManager lockManager,
        string taskId,
        bool bypassConfirmation = false,
        IToolRegistry? registry = null,
        bool fileLocksEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(lockManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        _inner = inner;
        _lockManager = lockManager;
        _taskId = taskId;
        _bypassConfirmation = bypassConfirmation;
        _registry = registry;
        _fileLocksEnabled = fileLocksEnabled;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName, JsonElement input, CancellationToken ct = default)
    {
        // Check if this is a write tool with a known file-path property.
        if (s_writeTools.TryGetValue(toolName, out var writeTool))
        {
            var filePath = input.ValueKind == JsonValueKind.Object
                && input.TryGetProperty(writeTool.PathProperty, out var fp)
                && fp.ValueKind == JsonValueKind.String
                    ? fp.GetString()
                    : null;

            if (!string.IsNullOrWhiteSpace(filePath) && writeTool.RestrictToWorkingDirectory)
            {
                // Path traversal guard: resolved path must stay under the current working directory.
                var resolved = Path.GetFullPath(filePath);
                var cwd = Path.GetFullPath(Environment.CurrentDirectory);
                if (!resolved.StartsWith(cwd + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(resolved, cwd, StringComparison.OrdinalIgnoreCase))
                {
                    return new ToolExecutionResult(
                        false,
                        $"Blocked: path '{filePath}' resolves outside the working directory.",
                        IsError: true);
                }
            }

            if (!string.IsNullOrWhiteSpace(filePath) && _fileLocksEnabled)
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

        // When bypass is enabled and we have a registry, execute the tool directly
        // without going through the inner executor's confirmation flow.
        if (_bypassConfirmation && _registry is not null)
        {
            if (!_registry.TryGetHandler(toolName, out var handler) || handler is null)
                return new ToolExecutionResult(false, $"Unknown tool: {toolName}", IsError: true);

            try
            {
                var output = await handler(input, ct).ConfigureAwait(false);
                return new ToolExecutionResult(true, output);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                return new ToolExecutionResult(false, $"{ex.GetType().Name}: {ex.Message}", IsError: true);
            }
        }

        return await _inner.ExecuteAsync(toolName, input, ct).ConfigureAwait(false);
    }
}
