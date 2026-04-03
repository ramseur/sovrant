using System.Text.Json;
using Sovrant.Tools.Todo;

namespace Sovrant.Tools.Tests.Todo;

public sealed class TodoWriteToolTests
{
    private static JsonElement MakeInput(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task Write_SetsItemsAndReturnsRenderedList()
    {
        var state = new TodoState();
        var tool = new TodoWriteTool(state);

        var input = MakeInput(new
        {
            todos = new[]
            {
                new { id = "1", content = "Write tests", status = "in_progress", priority = "high" },
                new { id = "2", content = "Push code",   status = "pending",     priority = "medium" },
            },
        });

        var result = await tool.ExecuteAsync(input);

        Assert.Contains("Write tests", result, StringComparison.Ordinal);
        Assert.Contains("Push code", result, StringComparison.Ordinal);
        Assert.Contains("[~]", result, StringComparison.Ordinal); // in_progress icon
        Assert.Contains("[ ]", result, StringComparison.Ordinal); // pending icon
    }

    [Fact]
    public async Task Write_CompletedItem_ShowsCheckmark()
    {
        var state = new TodoState();
        var tool = new TodoWriteTool(state);

        var input = MakeInput(new
        {
            todos = new[]
            {
                new { id = "1", content = "Done task", status = "completed", priority = "low" },
            },
        });

        var result = await tool.ExecuteAsync(input);

        Assert.Contains("[x]", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Write_ReplacesExistingList()
    {
        var state = new TodoState();
        var tool = new TodoWriteTool(state);

        await tool.ExecuteAsync(MakeInput(new
        {
            todos = new[] { new { id = "1", content = "Old task", status = "pending", priority = "low" } },
        }));

        await tool.ExecuteAsync(MakeInput(new
        {
            todos = new[] { new { id = "2", content = "New task", status = "pending", priority = "high" } },
        }));

        var items = state.Items;
        Assert.Single(items);
        Assert.Equal("New task", items[0].Content);
    }

    [Fact]
    public async Task Write_MissingTodosArray_ReturnsError()
    {
        var state = new TodoState();
        var tool = new TodoWriteTool(state);

        var result = await tool.ExecuteAsync(MakeInput(new { }));

        Assert.StartsWith("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Write_EmptyArray_ClearsList()
    {
        var state = new TodoState();
        var tool = new TodoWriteTool(state);

        // First add some items
        await tool.ExecuteAsync(MakeInput(new
        {
            todos = new[] { new { content = "Task", status = "pending", priority = "low" } },
        }));

        // Then clear
        var result = await tool.ExecuteAsync(MakeInput(new { todos = Array.Empty<object>() }));

        Assert.Contains("0 items", result, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.Items);
    }
}
