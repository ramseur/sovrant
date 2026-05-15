using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>A content block in a response from the model.</summary>
[JsonConverter(typeof(OutputContentBlockConverter))]
public abstract record OutputContentBlock
{
    private OutputContentBlock() { }

    /// <summary>A text output block.</summary>
    public sealed record TextBlock(
        [property: JsonPropertyName("text")] string Text) : OutputContentBlock;

    /// <summary>A tool use output block representing a tool invocation.</summary>
    public sealed record ToolUseBlock(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("input")] JsonElement Input) : OutputContentBlock;

    /// <summary>A thinking output block (extended thinking).</summary>
    public sealed record ThinkingBlock(
        [property: JsonPropertyName("thinking")] string Thinking,
        [property: JsonPropertyName("signature")] string? Signature = null) : OutputContentBlock;

    /// <summary>A redacted thinking output block.</summary>
    public sealed record RedactedThinkingBlock(
        [property: JsonPropertyName("data")] JsonElement Data) : OutputContentBlock;
}

internal sealed class OutputContentBlockConverter : JsonConverter<OutputContentBlock>
{
    public override OutputContentBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetProperty("type").GetString() switch
        {
            "text" => doc.RootElement.Deserialize<OutputContentBlock.TextBlock>(options),
            "tool_use" => doc.RootElement.Deserialize<OutputContentBlock.ToolUseBlock>(options),
            "thinking" => doc.RootElement.Deserialize<OutputContentBlock.ThinkingBlock>(options),
            "redacted_thinking" => doc.RootElement.Deserialize<OutputContentBlock.RedactedThinkingBlock>(options),
            var t => throw new JsonException($"Unknown content block type: {t}")
        };
    }

    public override void Write(Utf8JsonWriter writer, OutputContentBlock value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case OutputContentBlock.TextBlock t:
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteString("text", t.Text);
                writer.WriteEndObject();
                break;
            case OutputContentBlock.ToolUseBlock tu:
                writer.WriteStartObject();
                writer.WriteString("type", "tool_use");
                writer.WriteString("id", tu.Id);
                writer.WriteString("name", tu.Name);
                writer.WritePropertyName("input");
                tu.Input.WriteTo(writer);
                writer.WriteEndObject();
                break;
            case OutputContentBlock.ThinkingBlock th:
                writer.WriteStartObject();
                writer.WriteString("type", "thinking");
                writer.WriteString("thinking", th.Thinking);
                if (th.Signature is not null) writer.WriteString("signature", th.Signature);
                writer.WriteEndObject();
                break;
            case OutputContentBlock.RedactedThinkingBlock rt:
                writer.WriteStartObject();
                writer.WriteString("type", "redacted_thinking");
                writer.WritePropertyName("data");
                rt.Data.WriteTo(writer);
                writer.WriteEndObject();
                break;
            default:
                throw new JsonException($"Unknown OutputContentBlock type: {value.GetType().Name}");
        }
    }
}
