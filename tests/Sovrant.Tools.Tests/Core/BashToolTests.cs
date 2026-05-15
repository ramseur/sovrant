using System.Text.Json;
using Sovrant.Tools.Core;

namespace Sovrant.Tools.Tests.Core;

/// <summary>
/// Tests for BashTool. All tests are skipped when a functional bash shell is not
/// available in the test process (e.g. Windows without WSL or Git Bash on PATH).
/// </summary>
public sealed class BashToolTests
{
    /// <summary>Returns true when bash can execute a trivial echo command successfully.</summary>
    private static async Task<bool> IsBashFunctionalAsync()
    {
        try
        {
            var tool = new BashTool();
            var result = await tool.ExecuteAsync(MakeInput(new { command = "echo __probe__", timeout = 5000 }));
            return result.Contains("__probe__", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static JsonElement MakeInput(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task Bash_Echo_ReturnsOutput()
    {
        if (!await IsBashFunctionalAsync()) return;
        var tool = new BashTool();
        var result = await tool.ExecuteAsync(MakeInput(new { command = "echo hello" }));
        Assert.Contains("hello", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bash_Stderr_IncludedInOutput()
    {
        if (!await IsBashFunctionalAsync()) return;
        var tool = new BashTool();
        var result = await tool.ExecuteAsync(MakeInput(new { command = "echo errtxt >&2" }));
        Assert.Contains("errtxt", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bash_NonZeroExitCode_ReportedInOutput()
    {
        if (!await IsBashFunctionalAsync()) return;
        var tool = new BashTool();
        var result = await tool.ExecuteAsync(MakeInput(new { command = "exit 42" }));
        Assert.Contains("42", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bash_Timeout_ReturnsTimeoutError()
    {
        if (!await IsBashFunctionalAsync()) return;
        var tool = new BashTool();
        // 200 ms timeout, command sleeps 5 s
        var result = await tool.ExecuteAsync(MakeInput(new { command = "sleep 5", timeout = 200 }));
        Assert.Contains("timed out", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bash_MissingCommand_ReturnsError()
    {
        var tool = new BashTool();
        var result = await tool.ExecuteAsync(MakeInput(new { }));
        Assert.StartsWith("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Phase 43 — the shell's working directory must persist across
    /// BashTool invocations that share a <see cref="ShellSessionState"/>.
    /// Before the fix, every call inherited the Sovrant process cwd and
    /// <c>cd</c> was a no-op across invocations.
    /// </summary>
    [Fact]
    public async Task Bash_CdPersistsAcrossInvocations()
    {
        if (!await IsBashFunctionalAsync()) return;

        // Create two sibling directories we can cd between. Using real dirs
        // under TempPath rather than mocking so we also exercise the
        // "directory still exists?" check in BashTool.
        var root = Path.Combine(Path.GetTempPath(), $"sovrant_bash_cwd_{Guid.NewGuid():N}");
        var subA = Path.Combine(root, "a");
        var subB = Path.Combine(root, "b");
        Directory.CreateDirectory(subA);
        Directory.CreateDirectory(subB);

        try
        {
            var state = new ShellSessionState { CurrentDirectory = root };
            var tool = new BashTool(state);

            // Step 1: cd into subdir a. The sentinel line should be stripped
            // from the returned output; the state should record subA.
            var r1 = await tool.ExecuteAsync(MakeInput(new { command = "cd a ; echo step1" }));
            Assert.Contains("step1", r1, StringComparison.Ordinal);
            Assert.DoesNotContain("__SOVRANT_CWD_SENTINEL__", r1, StringComparison.Ordinal);
            AssertPathsEqual(subA, state.CurrentDirectory);

            // Step 2: run pwd from the new cwd — proves the second process
            // inherited the updated state rather than the original root.
            var r2 = await tool.ExecuteAsync(MakeInput(new { command = OperatingSystem.IsWindows() ? "(Get-Location).Path" : "pwd" }));
            Assert.Contains(Path.GetFileName(subA), r2, StringComparison.OrdinalIgnoreCase);

            // Step 3: traverse back up and over to sibling b.
            var r3 = await tool.ExecuteAsync(MakeInput(new { command = "cd ../b ; echo step3" }));
            Assert.Contains("step3", r3, StringComparison.Ordinal);
            AssertPathsEqual(subB, state.CurrentDirectory);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// If the tracked cwd is deleted out from under us, BashTool should
    /// fall back to the process cwd rather than fail to spawn.
    /// </summary>
    [Fact]
    public async Task Bash_DeletedCwd_FallsBackToProcessCwd()
    {
        if (!await IsBashFunctionalAsync()) return;

        var doomed = Path.Combine(Path.GetTempPath(), $"sovrant_bash_doomed_{Guid.NewGuid():N}");
        Directory.CreateDirectory(doomed);
        var state = new ShellSessionState { CurrentDirectory = doomed };

        // Remove the tracked cwd before the next call runs.
        Directory.Delete(doomed);

        var tool = new BashTool(state);
        var result = await tool.ExecuteAsync(MakeInput(new { command = "echo recovered" }));
        Assert.Contains("recovered", result, StringComparison.Ordinal);
        // State should now reflect the process cwd (or some other valid dir).
        Assert.True(Directory.Exists(state.CurrentDirectory));
    }

    private static void AssertPathsEqual(string expected, string actual)
    {
        // Normalize both sides: resolve symlinks/8.3 shortnames that Windows'
        // %TEMP% may introduce, and trim trailing separators.
        var e = Path.GetFullPath(expected).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var a = Path.GetFullPath(actual).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.Equal(e, a, ignoreCase: OperatingSystem.IsWindows());
    }
}
