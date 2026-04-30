using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Tools;

namespace Sovrant.Mcp.Tests;

public sealed class ToolBridgeTests
{
    [Fact]
    public void ConvertArguments_NullReturnsEmptyObject()
    {
        var result = McpServerSetup.ConvertArguments(null);
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Empty(result.EnumerateObject().ToList());
    }

    [Fact]
    public void ConvertArguments_EmptyDictReturnsEmptyObject()
    {
        var result = McpServerSetup.ConvertArguments(new Dictionary<string, JsonElement>());
        Assert.Equal(JsonValueKind.Object, result.ValueKind);
    }

    [Fact]
    public void ConvertArguments_PreservesStringValues()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["path"] = JsonDocument.Parse("\"hello.txt\"").RootElement,
        };
        var result = McpServerSetup.ConvertArguments(dict);
        Assert.Equal("hello.txt", result.GetProperty("path").GetString());
    }

    [Fact]
    public void ConvertArguments_PreservesNumericValues()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["count"] = JsonDocument.Parse("42").RootElement,
        };
        var result = McpServerSetup.ConvertArguments(dict);
        Assert.Equal(42, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public void ConvertArguments_PreservesBoolValues()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["verbose"] = JsonDocument.Parse("true").RootElement,
        };
        var result = McpServerSetup.ConvertArguments(dict);
        Assert.True(result.GetProperty("verbose").GetBoolean());
    }

    [Fact]
    public void ConvertArguments_PreservesNestedObjects()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["nested"] = JsonDocument.Parse("""{"a":1}""").RootElement,
        };
        var result = McpServerSetup.ConvertArguments(dict);
        Assert.Equal(1, result.GetProperty("nested").GetProperty("a").GetInt32());
    }
}
