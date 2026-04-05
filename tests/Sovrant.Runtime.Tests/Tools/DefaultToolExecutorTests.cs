using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Types;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Tests.Tools;

/// <summary>Tests for <see cref="DefaultToolExecutor"/>.</summary>
public sealed class DefaultToolExecutorTests
{
    private static JsonElement EmptyInput => JsonDocument.Parse("{}").RootElement;

    private static ToolDefinition MakeDefinition(string name) =>
        new(name, EmptyInput);

    private static DefaultToolExecutor CreateExecutor(
        PermissionMode mode,
        IToolRegistry? registry = null)
    {
        var policy = new ModeAwarePermissionPolicy(mode);
        registry ??= new InMemoryToolRegistry();
        return new DefaultToolExecutor(registry, policy, new NullGovernanceMonitor(), NullLogger<DefaultToolExecutor>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_AllowedTool_ReturnsSuccess()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(MakeDefinition("safe_tool"), (_, _) => Task.FromResult("done"));

        var executor = CreateExecutor(PermissionMode.DontAsk, registry);
        var result = await executor.ExecuteAsync("safe_tool", EmptyInput);

        Assert.True(result.Success);
        Assert.Equal("done", result.Output);
        Assert.False(result.IsError);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTool_ReturnsError()
    {
        var executor = CreateExecutor(PermissionMode.DontAsk);
        var result = await executor.ExecuteAsync("unknown_tool", EmptyInput);

        Assert.False(result.Success);
        Assert.True(result.IsError);
        Assert.Contains("Unknown tool", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_PlanMode_DeniesDestructiveTool()
    {
        var executor = CreateExecutor(PermissionMode.Plan);
        var result = await executor.ExecuteAsync("bash", EmptyInput);

        Assert.False(result.Success);
        Assert.True(result.IsError);
        Assert.Contains("blocked", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultMode_RequiresConfirmationForBash()
    {
        var executor = CreateExecutor(PermissionMode.Default);
        var result = await executor.ExecuteAsync("bash", EmptyInput);

        Assert.False(result.Success);
        Assert.True(result.IsError);
        Assert.Contains("confirmation", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
