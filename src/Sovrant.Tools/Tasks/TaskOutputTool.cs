using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Tasks;

/// <summary>Returns the full output of a background task.</summary>
public sealed class TaskOutputTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TaskOutput", CreateSchema())
    {
        Description = "Returns the complete stdout/stderr output of a background task.",
    };

    private readonly BackgroundTaskRegistry _registry;

    public TaskOutputTool(BackgroundTaskRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var taskId = GetString(input, "task_id");
        if (string.IsNullOrWhiteSpace(taskId))
            return Task.FromResult("Error: task_id is required.");

        if (!_registry.TryGet(taskId, out var info) || info is null)
            return Task.FromResult($"Error: task '{taskId}' not found.");

        string output;
        lock (info.OutputBuffer) { output = info.OutputBuffer.ToString(); }

        return Task.FromResult(string.IsNullOrEmpty(output) ? "(no output yet)" : output);
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "task_id": {"type": "string", "description": "The task ID returned by TaskCreate."}
            },
            "required": ["task_id"]
        }
        """).RootElement;
}
