using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Tasks;

/// <summary>Updates the description of an existing background task.</summary>
public sealed class TaskUpdateTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TaskUpdate", CreateSchema())
    {
        Description = "Updates the description of a background task created with TaskCreate.",
    };

    private readonly BackgroundTaskRegistry _registry;

    public TaskUpdateTool(BackgroundTaskRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var taskId = GetString(input, "task_id");
        if (string.IsNullOrWhiteSpace(taskId))
            return Task.FromResult("Error: task_id is required.");

        var description = GetString(input, "description");
        if (string.IsNullOrWhiteSpace(description))
            return Task.FromResult("Error: description is required.");

        if (!_registry.TryGet(taskId, out var info) || info is null)
            return Task.FromResult($"Error: task '{taskId}' not found.");

        info.Description = description;
        return Task.FromResult($"Task '{taskId}' description updated.");
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "task_id":     {"type": "string", "description": "The task ID to update."},
                "description": {"type": "string", "description": "New description for the task."}
            },
            "required": ["task_id", "description"]
        }
        """).RootElement;
}
