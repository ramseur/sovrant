using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>A discriminated union of all SSE stream events from the LLM API.</summary>
[JsonConverter(typeof(StreamEventConverter))]
public abstract record StreamEvent
{
    private StreamEvent() { }

    /// <summary>The message_start event, emitted first with initial message metadata.</summary>
    public sealed record MessageStart(
        [property: JsonPropertyName("message")] MessageResponse Message) : StreamEvent;

    /// <summary>The message_delta event, emitted when the stop_reason changes.</summary>
    public sealed record MessageDelta(
        [property: JsonPropertyName("delta")] Types.MessageDelta Delta,
        [property: JsonPropertyName("usage")] Usage Usage) : StreamEvent;

    /// <summary>The content_block_start event, emitted at the beginning of a content block.</summary>
    public sealed record ContentBlockStart(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("content_block")] OutputContentBlock ContentBlock) : StreamEvent;

    /// <summary>The content_block_delta event, emitted for each incremental content update.</summary>
    public sealed record ContentBlockDelta(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("delta")] Types.ContentBlockDelta Delta) : StreamEvent;

    /// <summary>The content_block_stop event, emitted at the end of a content block.</summary>
    public sealed record ContentBlockStop(
        [property: JsonPropertyName("index")] int Index) : StreamEvent;

    /// <summary>The message_stop event, emitted when the complete message is done.</summary>
    public sealed record MessageStop : StreamEvent;
}

internal sealed class StreamEventConverter : JsonConverter<StreamEvent>
{
    public override StreamEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var type = doc.RootElement.GetProperty("type").GetString();
        return type switch
        {
            "message_start" => doc.RootElement.Deserialize<StreamEvent.MessageStart>(options),
            "message_delta" => doc.RootElement.Deserialize<StreamEvent.MessageDelta>(options),
            "content_block_start" => doc.RootElement.Deserialize<StreamEvent.ContentBlockStart>(options),
            "content_block_delta" => doc.RootElement.Deserialize<StreamEvent.ContentBlockDelta>(options),
            "content_block_stop" => doc.RootElement.Deserialize<StreamEvent.ContentBlockStop>(options),
            "message_stop" => new StreamEvent.MessageStop(),
            _ => throw new JsonException($"Unknown stream event type: {type}")
        };
    }

    public override void Write(Utf8JsonWriter writer, StreamEvent value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case StreamEvent.MessageStart ms:
                writer.WriteStartObject();
                writer.WriteString("type", "message_start");
                writer.WritePropertyName("message");
                JsonSerializer.Serialize(writer, ms.Message, options);
                writer.WriteEndObject();
                break;
            case StreamEvent.MessageDelta md:
                writer.WriteStartObject();
                writer.WriteString("type", "message_delta");
                writer.WritePropertyName("delta");
                JsonSerializer.Serialize(writer, md.Delta, options);
                writer.WritePropertyName("usage");
                JsonSerializer.Serialize(writer, md.Usage, options);
                writer.WriteEndObject();
                break;
            case StreamEvent.ContentBlockStart cbs:
                writer.WriteStartObject();
                writer.WriteString("type", "content_block_start");
                writer.WriteNumber("index", cbs.Index);
                writer.WritePropertyName("content_block");
                JsonSerializer.Serialize(writer, cbs.ContentBlock, options);
                writer.WriteEndObject();
                break;
            case StreamEvent.ContentBlockDelta cbd:
                writer.WriteStartObject();
                writer.WriteString("type", "content_block_delta");
                writer.WriteNumber("index", cbd.Index);
                writer.WritePropertyName("delta");
                JsonSerializer.Serialize(writer, cbd.Delta, options);
                writer.WriteEndObject();
                break;
            case StreamEvent.ContentBlockStop cbstop:
                writer.WriteStartObject();
                writer.WriteString("type", "content_block_stop");
                writer.WriteNumber("index", cbstop.Index);
                writer.WriteEndObject();
                break;
            case StreamEvent.MessageStop:
                writer.WriteStartObject();
                writer.WriteString("type", "message_stop");
                writer.WriteEndObject();
                break;
            default:
                throw new JsonException($"Unknown StreamEvent type: {value.GetType().Name}");
        }
    }
}
