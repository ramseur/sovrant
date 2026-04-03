using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>A content block in an input message.</summary>
[JsonConverter(typeof(InputContentBlockConverter))]
public abstract record InputContentBlock
{
    private InputContentBlock() { }
    /// <summary>A plain text input block.</summary>
    public sealed record TextBlock([property: JsonPropertyName("text")] string Text) : InputContentBlock;
    /// <summary>A tool use input block (assistant tool call).</summary>
    public sealed record ToolUseBlock(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("input")] JsonElement Input) : InputContentBlock;
    /// <summary>A tool result block (user response to assistant tool call).</summary>
    public sealed record ToolResultBlock(
        [property: JsonPropertyName("tool_use_id")] string ToolUseId,
        [property: JsonPropertyName("content")] IReadOnlyList<ToolResultContentBlock> Content,
        [property: JsonPropertyName("is_error")] bool IsError = false) : InputContentBlock;
}

internal sealed class InputContentBlockConverter : JsonConverter<InputContentBlock>
{
    public override InputContentBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetProperty("type").GetString() switch
        {
            "text" => doc.RootElement.Deserialize<InputContentBlock.TextBlock>(options),
            "tool_use" => doc.RootElement.Deserialize<InputContentBlock.ToolUseBlock>(options),
            "tool_result" => doc.RootElement.Deserialize<InputContentBlock.ToolResultBlock>(options),
            var t => throw new JsonException($"Unknown input content block type: {t}")
        };
    }
    public override void Write(Utf8JsonWriter writer, InputContentBlock value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case InputContentBlock.TextBlock t:
                writer.WriteStartObject(); writer.WriteString("type", "text"); writer.WriteString("text", t.Text); writer.WriteEndObject(); break;
            case InputContentBlock.ToolUseBlock tu:
                writer.WriteStartObject(); writer.WriteString("type", "tool_use"); writer.WriteString("id", tu.Id); writer.WriteString("name", tu.Name); writer.WritePropertyName("input"); tu.Input.WriteTo(writer); writer.WriteEndObject(); break;
            case InputContentBlock.ToolResultBlock tr:
                writer.WriteStartObject(); writer.WriteString("type", "tool_result"); writer.WriteString("tool_use_id", tr.ToolUseId); writer.WriteBoolean("is_error", tr.IsError); writer.WritePropertyName("content"); JsonSerializer.Serialize(writer, tr.Content, options); writer.WriteEndObject(); break;
            default: throw new JsonException($"Unknown InputContentBlock: {value.GetType().Name}");
        }
    }
}
