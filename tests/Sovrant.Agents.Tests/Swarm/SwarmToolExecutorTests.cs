using System.Text.Json;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Swarm;
using Sovrant.Runtime.Tools;

namespace Sovrant.Agents.Tests.Swarm;

/// <summary>
/// Verifies that <see cref="SwarmToolExecutor"/> enforces file locks on the real
/// write tool names (<c>Write</c>/<c>Edit</c>). It previously keyed on
/// <c>WriteFile</c>/<c>EditFile</c>, which no registered tool is called, so
/// concurrent swarm workers could overwrite each other's files unchecked.
/// </summary>
public sealed class SwarmToolExecutorTests
{
    // Absolute and outside the process's working directory, like a real project
    // folder targeted from Desktop/Web/Server.
    private static readonly string s_target =
        Path.Combine(Path.GetTempPath(), "sovrant-swarm-lock-test", "report.md");

    [Theory]
    [InlineData("Write")]
    [InlineData("Edit")]
    public async Task WriteTool_FileLockedByOtherTask_IsBlocked(string toolName)
    {
        var locks = new FileLockManager();
        locks.TryAcquire(s_target, "other-task");
        var inner = new RecordingExecutor();
        var executor = new SwarmToolExecutor(inner, locks, "this-task");

        var result = await executor.ExecuteAsync(toolName, FilePathInput(s_target));

        Assert.True(result.IsError);
        Assert.Contains("locked by task 'other-task'", result.Output, StringComparison.Ordinal);
        Assert.Empty(inner.Calls);
    }

    [Theory]
    [InlineData("Write")]
    [InlineData("Edit")]
    public async Task WriteTool_UnlockedFile_AcquiresLockAndRuns(string toolName)
    {
        var locks = new FileLockManager();
        var inner = new RecordingExecutor();
        var executor = new SwarmToolExecutor(inner, locks, "this-task");

        var result = await executor.ExecuteAsync(toolName, FilePathInput(s_target));

        Assert.False(result.IsError);
        Assert.Equal([toolName], inner.Calls);
        Assert.Equal("this-task", locks.GetHolder(s_target));
    }

    [Fact]
    public async Task WriteTool_LockedFile_RelativeDeclarationMatchesAbsoluteWrite()
    {
        // Declared FilesToModify entries can be relative; tools write absolute paths.
        var locks = new FileLockManager();
        locks.TryAcquire("declared.md", "other-task");
        var executor = new SwarmToolExecutor(new RecordingExecutor(), locks, "this-task");

        var result = await executor.ExecuteAsync("Write", FilePathInput(Path.GetFullPath("declared.md")));

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task WriteTool_FileLocksDisabled_LockManagerIgnored()
    {
        var locks = new FileLockManager();
        locks.TryAcquire(s_target, "other-task");
        var inner = new RecordingExecutor();
        var executor = new SwarmToolExecutor(inner, locks, "this-task", fileLocksEnabled: false);

        var result = await executor.ExecuteAsync("Write", FilePathInput(s_target));

        Assert.False(result.IsError);
        Assert.Equal(["Write"], inner.Calls);
        Assert.Equal("other-task", locks.GetHolder(s_target));
    }

    [Fact]
    public async Task Write_OutsideWorkingDirectory_IsNotBlocked()
    {
        // Write/Edit are not restricted to the process's working directory —
        // that boundary belongs to Phase 124, not the swarm lock layer.
        var inner = new RecordingExecutor();
        var executor = new SwarmToolExecutor(inner, new FileLockManager(), "this-task");

        var result = await executor.ExecuteAsync("Write", FilePathInput(s_target));

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task NotebookEdit_OutsideWorkingDirectory_IsStillBlocked()
    {
        var inner = new RecordingExecutor();
        var executor = new SwarmToolExecutor(inner, new FileLockManager(), "this-task");
        var input = JsonDocument.Parse(JsonSerializer.Serialize(new { notebook_path = s_target })).RootElement;

        var result = await executor.ExecuteAsync("NotebookEdit", input);

        Assert.True(result.IsError);
        Assert.Contains("outside the working directory", result.Output, StringComparison.Ordinal);
        Assert.Empty(inner.Calls);
    }

    [Fact]
    public async Task ReadTool_IsNotLocked()
    {
        var locks = new FileLockManager();
        locks.TryAcquire(s_target, "other-task");
        var inner = new RecordingExecutor();
        var executor = new SwarmToolExecutor(inner, locks, "this-task");

        var result = await executor.ExecuteAsync("Read", FilePathInput(s_target));

        Assert.False(result.IsError);
        Assert.Equal(["Read"], inner.Calls);
    }

    private static JsonElement FilePathInput(string path) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { file_path = path })).RootElement;

    private sealed class RecordingExecutor : IToolExecutor
    {
        public List<string> Calls { get; } = [];

        public Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct = default)
        {
            Calls.Add(toolName);
            return Task.FromResult(new ToolExecutionResult(true, "ok"));
        }
    }
}
