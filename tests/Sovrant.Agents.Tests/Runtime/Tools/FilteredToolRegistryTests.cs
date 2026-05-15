using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Tools;

namespace Sovrant.Agents.Tests.Runtime.Tools;

public sealed class FilteredToolRegistryTests
{
    private static ToolDefinition MakeDefinition(string name) =>
        new(name, JsonDocument.Parse("{}").RootElement);

    [Fact]
    public void GetDefinitions_Filters_To_Allowed_Set()
    {
        var inner = new InMemoryToolRegistry();
        inner.Register(MakeDefinition("ReadFile"), (_, _) => Task.FromResult("ok"));
        inner.Register(MakeDefinition("BashTool"), (_, _) => Task.FromResult("ok"));
        inner.Register(MakeDefinition("WebFetch"), (_, _) => Task.FromResult("ok"));

        var allowed = new HashSet<string>(StringComparer.Ordinal) { "ReadFile", "WebFetch" };
        var filtered = new FilteredToolRegistry(inner, allowed);

        var defs = filtered.GetDefinitions();

        Assert.Equal(2, defs.Count);
        Assert.Contains(defs, d => d.Name == "ReadFile");
        Assert.Contains(defs, d => d.Name == "WebFetch");
        Assert.DoesNotContain(defs, d => d.Name == "BashTool");
    }

    [Fact]
    public void TryGetHandler_Blocks_Disallowed_Tools()
    {
        var inner = new InMemoryToolRegistry();
        inner.Register(MakeDefinition("BashTool"), (_, _) => Task.FromResult("ok"));

        var allowed = new HashSet<string>(StringComparer.Ordinal) { "ReadFile" };
        var filtered = new FilteredToolRegistry(inner, allowed);

        Assert.False(filtered.TryGetHandler("BashTool", out var handler));
        Assert.Null(handler);
    }

    [Fact]
    public void TryGetHandler_Allows_Allowed_Tools()
    {
        var inner = new InMemoryToolRegistry();
        inner.Register(MakeDefinition("ReadFile"), (_, _) => Task.FromResult("ok"));

        var allowed = new HashSet<string>(StringComparer.Ordinal) { "ReadFile" };
        var filtered = new FilteredToolRegistry(inner, allowed);

        Assert.True(filtered.TryGetHandler("ReadFile", out var handler));
        Assert.NotNull(handler);
    }

    [Fact]
    public void Register_Delegates_To_Inner()
    {
        var inner = new InMemoryToolRegistry();
        var filtered = new FilteredToolRegistry(inner, new HashSet<string>(StringComparer.Ordinal) { "NewTool" });

        filtered.Register(MakeDefinition("NewTool"), (_, _) => Task.FromResult("ok"));

        // Should be visible through inner
        Assert.True(inner.TryGetHandler("NewTool", out _));
        // Should be visible through filtered (it's in the allowed set)
        Assert.True(filtered.TryGetHandler("NewTool", out _));
    }
}
