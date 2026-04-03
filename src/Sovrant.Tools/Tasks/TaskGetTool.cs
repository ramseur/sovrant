using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Tasks;

/// <summary>Returns the status and a brief output preview of a background task.</summary>
public sealed class TaskGetTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TaskGet", CreateSchema())
    {
        Description = "Returns the current status and output preview of a background task created with TaskCreate.",
    };

    private readonly BackgroundTaskRegistry _registry;

    public TaskGetTool(BackgroundTaskRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var taskId = GetString(input, "task_id");
        if (string.IsNullOrWhiteSpace(taskId))
            return Task.FromResult("Error: task_id is required.");

        if (!_registry.TryGet(taskId, out var info) || info is null)
            return Task.FromResult($"Error: task '{taskId}' not found.");

        string outputPreview;
        lock (info.OutputBuffer)
        {
            var output = info.OutputBuffer.ToString();
            outputPreview = output.Length > 500 ? $"...{output[^500..]}" : output;
        }

        var duration = (info.CompletedAt ?? DateTimeOffset.UtcNow) - info.StartedAt;
        var result =
            $"Task ID: {info.Id}\n" +
            $"Status:  {info.Status}\n" +
            $"Command: {info.Command}\n" +
            $"Duration: {duration.TotalSeconds:F1}s\n" +
            (info.ExitCode.HasValue ? $"Exit code: {info.ExitCode}\n" : string.Empty) +
            (string.IsNullOrWhiteSpace(outputPreview) ? string.Empty : $"\n[Output preview]\n{outputPreview}");

        return Task.FromResult(result.TrimEnd());
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
