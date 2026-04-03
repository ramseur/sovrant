using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Tasks;

/// <summary>Cancels a running background task.</summary>
public sealed class TaskStopTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TaskStop", CreateSchema())
    {
        Description = "Cancels a running background task created with TaskCreate.",
    };

    private readonly BackgroundTaskRegistry _registry;

    public TaskStopTool(BackgroundTaskRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var taskId = GetString(input, "task_id");
        if (string.IsNullOrWhiteSpace(taskId))
            return "Error: task_id is required.";

        if (!_registry.TryGet(taskId, out var info) || info is null)
            return $"Error: task '{taskId}' not found.";

        if (info.Status != BackgroundTaskStatus.Running)
            return $"Task '{taskId}' is already {info.Status}.";

        await info.Cts.CancelAsync().ConfigureAwait(false);
        return $"Cancellation requested for task '{taskId}'.";
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "task_id": {"type": "string", "description": "The task ID to cancel."}
            },
            "required": ["task_id"]
        }
        """).RootElement;
}
