using System.Text.Json;

namespace Sovrant.Mcp.Tests;

public sealed class ChatToolHandlerTests
{
    [Fact]
    public void Definition_HasCorrectName()
    {
        Assert.Equal("chat", ChatToolHandler.Definition.Name);
    }

    [Fact]
    public void Definition_HasDescription()
    {
        Assert.NotNull(ChatToolHandler.Definition.Description);
        Assert.Contains("agentic", ChatToolHandler.Definition.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Definition_SchemaRequiresMessage()
    {
        var schema = ChatToolHandler.Definition.InputSchema;
        Assert.Equal(JsonValueKind.Object, schema.ValueKind);

        var required = schema.GetProperty("required");
        Assert.Equal(JsonValueKind.Array, required.ValueKind);
        Assert.Contains("message", required.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Definition_SchemaHasSessionIdProperty()
    {
        var schema = ChatToolHandler.Definition.InputSchema;
        var props = schema.GetProperty("properties");
        Assert.True(props.TryGetProperty("session_id", out _));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyArguments_ReturnsError()
    {
        // No IServiceProvider needed — should fail before resolving services.
        var result = await ChatToolHandler.ExecuteAsync(
            new FakeServiceProvider(),
            null,
            CancellationToken.None);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyMessage_ReturnsError()
    {
        var args = new Dictionary<string, JsonElement>
        {
            ["message"] = JsonDocument.Parse("\"  \"").RootElement,
        };

        var result = await ChatToolHandler.ExecuteAsync(
            new FakeServiceProvider(),
            args,
            CancellationToken.None);

        Assert.True(result.IsError);
    }

    /// <summary>Minimal service provider that throws on any resolve — used to test early validation.</summary>
    private sealed class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
