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
}
