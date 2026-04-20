using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Config;
using Sovrant.Agents.Isolated;
using Sovrant.Agents.Models;

namespace Sovrant.Agents.Tests.Isolated;

public sealed class ProcessBasedOrchestrationSystemTests : IDisposable
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static string ShellCommand => IsWindows ? "cmd.exe" : "/bin/sh";
    private static string EchoArgs(string text) => IsWindows ? $"/c echo {text}" : $"-c \"echo {text}\"";

    private readonly AgentSystemConfig _config = new() { TaskTimeoutSeconds = 5 };
    private readonly ProcessBasedOrchestrationSystem _system;

    public ProcessBasedOrchestrationSystemTests()
    {
        _system = new ProcessBasedOrchestrationSystem(_config, NullLogger<ProcessBasedOrchestrationSystem>.Instance);
    }

    public void Dispose() => _system.Dispose();

    [Fact]
    public async Task RunTaskAsync_Dispatches_To_Named_Agent()
    {
        var psi = new ProcessStartInfo(ShellCommand, EchoArgs("hello"));
        var agent = new ProcessAgent("echo", psi, NullLogger<ProcessAgent>.Instance);
        _system.RegisterAgent(agent);

        var task = AgentTask.Create("test", "echo");
        var result = await _system.RunTaskAsync(task);

        Assert.True(result.Success);
        Assert.Contains("hello", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunTaskAsync_Unknown_Agent_Returns_Fail()
    {
        var task = AgentTask.Create("test", "nonexistent");
        var result = await _system.RunTaskAsync(task);

        Assert.False(result.Success);
        Assert.Contains("Unknown agent", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTaskAsync_No_Agents_Falls_Back_Fail()
    {
        var task = AgentTask.Create("test");
        var result = await _system.RunTaskAsync(task);

        Assert.False(result.Success);
        Assert.Contains("No agents registered", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShutdownAsync_Completes()
    {
        await _system.ShutdownAsync();
        // Should not throw
    }
}
