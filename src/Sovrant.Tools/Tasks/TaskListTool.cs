using System.Globalization;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Tasks;

/// <summary>Lists all background tasks in the current session.</summary>
public sealed class TaskListTool : ITool
{
    private static readonly ToolDefinition s_definition = new("TaskList", CreateSchema())
    {
        Description = "Lists all background tasks created with TaskCreate in the current session.",
    };

    private readonly BackgroundTaskRegistry _registry;

    public TaskListTool(BackgroundTaskRegistry registry) => _registry = registry;

    public ToolDefinition Definition => s_definition;

    public Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var tasks = _registry.All;
        if (tasks.Count == 0)
            return Task.FromResult("No background tasks.");

        var sb = new StringBuilder($"{tasks.Count} background task(s):\n\n");
        foreach (var t in tasks.OrderByDescending(t => t.StartedAt))
        {
            var dur = (t.CompletedAt ?? DateTimeOffset.UtcNow) - t.StartedAt;
            sb.AppendLine(CultureInfo.InvariantCulture, $"[{t.Id}] {t.Status,-12} {dur.TotalSeconds:F1}s  {t.Description}");
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {"type": "object", "properties": {}, "required": []}
        """).RootElement;
}
