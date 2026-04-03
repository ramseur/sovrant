using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>Controls how the model selects tools to use.</summary>
[JsonConverter(typeof(ToolChoiceConverter))]
public abstract record ToolChoice
{
    private ToolChoice() { }

    /// <summary>The model automatically decides whether to use a tool.</summary>
    public sealed record Auto : ToolChoice;

    /// <summary>The model must use one of the available tools.</summary>
    public sealed record Any : ToolChoice;

    /// <summary>The model must use the specified tool.</summary>
    /// <param name="ToolName">The name of the tool to use.</param>
    public sealed record Tool(string ToolName) : ToolChoice;
}

internal sealed class ToolChoiceConverter : JsonConverter<ToolChoice>
{
    public override ToolChoice? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetProperty("type").GetString() switch
        {
            "auto" => new ToolChoice.Auto(),
            "any" => new ToolChoice.Any(),
            "tool" => new ToolChoice.Tool(doc.RootElement.GetProperty("name").GetString()!),
            var t => throw new JsonException($"Unknown tool_choice type: {t}")
        };
    }

    public override void Write(Utf8JsonWriter writer, ToolChoice value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case ToolChoice.Auto:
                writer.WriteString("type", "auto");
                break;
            case ToolChoice.Any:
                writer.WriteString("type", "any");
                break;
            case ToolChoice.Tool t:
                writer.WriteString("type", "tool");
                writer.WriteString("name", t.ToolName);
                break;
            default:
                throw new JsonException($"Unknown ToolChoice type: {value.GetType().Name}");
        }
        writer.WriteEndObject();
    }
}
