using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Extended;

/// <summary>Pauses execution for the specified number of seconds.</summary>
public sealed class SleepTool : ITool
{
    private const double MaxSeconds = 60.0;

    private static readonly ToolDefinition s_definition = new("Sleep", CreateSchema())
    {
        Description = "Waits for the specified number of seconds (max 60) without holding a shell process.",
    };

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var seconds = GetDouble(input, "duration_seconds", 1.0);
        seconds = Math.Clamp(seconds, 0.0, MaxSeconds);

        await Task.Delay(TimeSpan.FromSeconds(seconds), ct).ConfigureAwait(false);
        return $"Slept for {seconds:F1} seconds.";
    }

    private static double GetDouble(JsonElement el, string prop, double def) =>
        el.TryGetProperty(prop, out var v) && v.TryGetDouble(out var d) ? d : def;

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "duration_seconds": {"type": "number", "description": "Number of seconds to wait (max 60)."}
            },
            "required": ["duration_seconds"]
        }
        """).RootElement;
}
