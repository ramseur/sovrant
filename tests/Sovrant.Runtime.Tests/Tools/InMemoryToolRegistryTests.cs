using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Tests.Tools;

/// <summary>Tests for <see cref="InMemoryToolRegistry"/>.</summary>
public sealed class InMemoryToolRegistryTests
{
    private static ToolDefinition MakeDefinition(string name) =>
        new(name, JsonDocument.Parse("{}").RootElement);

    [Fact]
    public void Register_And_GetDefinitions_ReturnsRegistered()
    {
        var registry = new InMemoryToolRegistry();
        var def = MakeDefinition("my_tool");
        registry.Register(def, (_, _) => Task.FromResult("ok"));

        var defs = registry.GetDefinitions();
        Assert.Single(defs);
        Assert.Equal("my_tool", defs[0].Name);
    }

    [Fact]
    public void TryGetHandler_KnownTool_ReturnsTrue()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(MakeDefinition("tool_a"), (_, _) => Task.FromResult("a"));

        Assert.True(registry.TryGetHandler("tool_a", out var handler));
        Assert.NotNull(handler);
    }

    [Fact]
    public void TryGetHandler_UnknownTool_ReturnsFalse()
    {
        var registry = new InMemoryToolRegistry();
        Assert.False(registry.TryGetHandler("nonexistent", out var handler));
        Assert.Null(handler);
    }

    [Fact]
    public void Register_Overwrites_ExistingTool()
    {
        var registry = new InMemoryToolRegistry();
        registry.Register(MakeDefinition("dup"), (_, _) => Task.FromResult("first"));
        registry.Register(MakeDefinition("dup"), (_, _) => Task.FromResult("second"));

        var defs = registry.GetDefinitions();
        Assert.Single(defs);
        Assert.Equal("dup", defs[0].Name);
    }

    [Fact]
    public void Register_IsSafe_UnderConcurrentWriters()
    {
        var registry = new InMemoryToolRegistry();
        const int writers = 16;
        const int perWriter = 64;

        Parallel.For(0, writers, w =>
        {
            for (var i = 0; i < perWriter; i++)
                registry.Register(MakeDefinition($"tool_{w}_{i}"), (_, _) => Task.FromResult("ok"));
        });

        Assert.Equal(writers * perWriter, registry.GetDefinitions().Count);
    }
}
