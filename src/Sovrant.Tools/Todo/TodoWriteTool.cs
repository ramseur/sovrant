using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Todo;

/// <summary>Replaces the in-session todo list with a new set of items.</summary>
public sealed class TodoWriteTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TodoWrite", CreateSchema())
    {
        Description =
            "Creates or replaces the in-session structured task list. " +
            "Pass an array of todo objects — each with id, content, status (pending/in_progress/completed), " +
            "and priority (low/medium/high). The full list is replaced on each call.",
    };

    private readonly TodoState _state;

    public TodoWriteTool(TodoState state) => _state = state;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        if (!input.TryGetProperty("todos", out var todosEl) ||
            todosEl.ValueKind != JsonValueKind.Array)
            return Task.FromResult("Error: todos array is required.");

        var items = new List<TodoItem>();
        foreach (var t in todosEl.EnumerateArray())
        {
            var id = t.GetStringProp("id", Guid.NewGuid().ToString("N")[..8]);
            var content = t.GetStringProp("content");
            var status = ParseEnum(t.GetStringProp("status", "pending"), TodoStatus.Pending);
            var priority = ParseEnum(t.GetStringProp("priority", "medium"), TodoPriority.Medium);
            items.Add(new TodoItem(id, content, status, priority));
        }

        _state.SetItems(items);

        var sb = new StringBuilder($"Todo list updated ({items.Count} items):\n");
        foreach (var item in items)
        {
            var icon = item.Status switch
            {
                TodoStatus.Completed => "[x]",
                TodoStatus.InProgress => "[~]",
                _ => "[ ]",
            };
            sb.AppendLine(CultureInfo.InvariantCulture, $"{icon} [{item.Priority}] {item.Content}");
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }


    private static TEnum ParseEnum<TEnum>(string value, TEnum defaultValue) where TEnum : struct =>
        Enum.TryParse<TEnum>(value.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var result)
            ? result : defaultValue;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "todos": {
                    "type": "array",
                    "description": "The complete todo list to set.",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id":       {"type": "string"},
                            "content":  {"type": "string"},
                            "status":   {"type": "string", "enum": ["pending","in_progress","completed"]},
                            "priority": {"type": "string", "enum": ["low","medium","high"]}
                        },
                        "required": ["content", "status", "priority"]
                    }
                }
            },
            "required": ["todos"]
        }
        """).RootElement;
}
