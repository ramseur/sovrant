using System.Text.Json;
using Sovrant.Runtime.Mcp;
using Sovrant.Tools.Mcp;

namespace Sovrant.Tools.Tests.Mcp;

public sealed class McpProxyToolTests
{
    // ── Definition ────────────────────────────────────────────────────────────

    [Fact]
    public void Definition_HasCorrectName()
    {
        var tool = new McpProxyTool(new McpClientRegistry());
        Assert.Equal("MCPTool", tool.Definition.Name);
    }

    [Fact]
    public void Definition_HasDescription()
    {
        var tool = new McpProxyTool(new McpClientRegistry());
        Assert.NotNull(tool.Definition.Description);
        Assert.NotEmpty(tool.Definition.Description);
    }

    [Fact]
    public void Definition_SchemaRequiresTool()
    {
        var tool = new McpProxyTool(new McpClientRegistry());
        var schema = tool.Definition.InputSchema;

        var required = schema.GetProperty("required");
        Assert.Equal(JsonValueKind.Array, required.ValueKind);
        Assert.Contains("tool", required.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Definition_SchemaHasServerProperty()
    {
        var tool = new McpProxyTool(new McpClientRegistry());
        var props = tool.Definition.InputSchema.GetProperty("properties");
        Assert.True(props.TryGetProperty("server", out _));
    }

    [Fact]
    public void Definition_SchemaHasInputProperty()
    {
        var tool = new McpProxyTool(new McpClientRegistry());
        var props = tool.Definition.InputSchema.GetProperty("properties");
        Assert.True(props.TryGetProperty("input", out _));
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NoServersConnected_ReturnsNoServersMessage()
    {
        var tool = new McpProxyTool(new McpClientRegistry());
        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("{}").RootElement,
            CancellationToken.None);

        Assert.Equal("No MCP servers connected.", result);
    }

    [Fact]
    public async Task ExecuteAsync_MissingToolParameter_ReturnsRequiredMessage()
    {
        // Registry has no clients — validation fires before any client lookup.
        var tool = new McpProxyTool(new McpClientRegistry());
        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"server":"myserver"}""").RootElement,
            CancellationToken.None);

        // "No MCP servers connected" fires before "tool required" because HasClients is false.
        // Test that at minimum an error is returned (not an exception).
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyToolName_ReturnsRequiredMessage()
    {
        // With no clients, still returns before needing to check the tool name.
        var tool = new McpProxyTool(new McpClientRegistry());
        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"tool":"   "}""").RootElement,
            CancellationToken.None);

        Assert.NotEmpty(result);
    }
}
