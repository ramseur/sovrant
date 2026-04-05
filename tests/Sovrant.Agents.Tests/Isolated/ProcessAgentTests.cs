using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Isolated;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Tests.Isolated;

public sealed class ProcessAgentTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static string ShellCommand => IsWindows ? "cmd.exe" : "/bin/sh";
    private static string EchoArgs(string text) => IsWindows ? $"/c echo {text}" : $"-c \"echo {text}\"";
    private static string FailArgs => IsWindows ? "/c exit 1" : "-c \"exit 1\"";
    private static string SleepArgs => IsWindows ? "/c ping -n 30 127.0.0.1 > nul" : "-c \"sleep 30\"";

    [Fact]
    public async Task HandleAsync_Collects_Stdout()
    {
        var psi = new ProcessStartInfo(ShellCommand, EchoArgs("hello"));
        var agent = new ProcessAgent("echo-agent", psi, NullLogger<ProcessAgent>.Instance);

        var task = AgentTask.Create("test");
        var result = await agent.HandleAsync(task);

        Assert.True(result.Success);
        Assert.Contains("hello", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_NonZero_ExitCode_Returns_Fail()
    {
        var psi = new ProcessStartInfo(ShellCommand, FailArgs);
        var agent = new ProcessAgent("fail-agent", psi, NullLogger<ProcessAgent>.Instance);

        var task = AgentTask.Create("fail");
        var result = await agent.HandleAsync(task);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task HandleAsync_Cancellation_Kills_Process()
    {
        var psi = new ProcessStartInfo(ShellCommand, SleepArgs);
        var agent = new ProcessAgent("sleep-agent", psi, NullLogger<ProcessAgent>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var task = AgentTask.Create("sleep");
        var result = await agent.HandleAsync(task, cts.Token);

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
