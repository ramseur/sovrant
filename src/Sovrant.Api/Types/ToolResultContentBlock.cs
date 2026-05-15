using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>Content block within a tool result.</summary>
[JsonConverter(typeof(ToolResultContentBlockConverter))]
public abstract record ToolResultContentBlock
{
    private ToolResultContentBlock() { }
    /// <summary>A text content block within a tool result.</summary>
    public sealed record TextBlock([property: JsonPropertyName("text")] string Text) : ToolResultContentBlock;
    /// <summary>An image content block within a tool result.</summary>
    public sealed record ImageBlock([property: JsonPropertyName("source")] JsonElement Source) : ToolResultContentBlock;
}

internal sealed class ToolResultContentBlockConverter : JsonConverter<ToolResultContentBlock>
{
    public override ToolResultContentBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetProperty("type").GetString() switch
        {
            "text" => doc.RootElement.Deserialize<ToolResultContentBlock.TextBlock>(options),
            "image" => doc.RootElement.Deserialize<ToolResultContentBlock.ImageBlock>(options),
            var t => throw new JsonException($"Unknown tool result content block type: {t}")
        };
    }
    public override void Write(Utf8JsonWriter writer, ToolResultContentBlock value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ToolResultContentBlock.TextBlock t:
                writer.WriteStartObject(); writer.WriteString("type", "text"); writer.WriteString("text", t.Text); writer.WriteEndObject(); break;
            case ToolResultContentBlock.ImageBlock img:
                writer.WriteStartObject(); writer.WriteString("type", "image"); writer.WritePropertyName("source"); img.Source.WriteTo(writer); writer.WriteEndObject(); break;
            default: throw new JsonException($"Unknown ToolResultContentBlock: {value.GetType().Name}");
        }
    }
}
