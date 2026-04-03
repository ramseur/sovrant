using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>A delta update for a content block during streaming.</summary>
[JsonConverter(typeof(ContentBlockDeltaConverter))]
public abstract record ContentBlockDelta
{
    private ContentBlockDelta() { }

    /// <summary>A text delta update.</summary>
    public sealed record TextDelta(
        [property: JsonPropertyName("text")] string Text) : ContentBlockDelta;

    /// <summary>A partial JSON delta for tool input.</summary>
    public sealed record InputJsonDelta(
        [property: JsonPropertyName("partial_json")] string PartialJson) : ContentBlockDelta;

    /// <summary>A thinking text delta update.</summary>
    public sealed record ThinkingDelta(
        [property: JsonPropertyName("thinking")] string Thinking) : ContentBlockDelta;

    /// <summary>A signature delta update for thinking blocks.</summary>
    public sealed record SignatureDelta(
        [property: JsonPropertyName("signature")] string Signature) : ContentBlockDelta;
}

internal sealed class ContentBlockDeltaConverter : JsonConverter<ContentBlockDelta>
{
    public override ContentBlockDelta? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetProperty("type").GetString() switch
        {
            "text_delta" => doc.RootElement.Deserialize<ContentBlockDelta.TextDelta>(options),
            "input_json_delta" => doc.RootElement.Deserialize<ContentBlockDelta.InputJsonDelta>(options),
            "thinking_delta" => doc.RootElement.Deserialize<ContentBlockDelta.ThinkingDelta>(options),
            "signature_delta" => doc.RootElement.Deserialize<ContentBlockDelta.SignatureDelta>(options),
            var t => throw new JsonException($"Unknown content block delta type: {t}")
        };
    }

    public override void Write(Utf8JsonWriter writer, ContentBlockDelta value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ContentBlockDelta.TextDelta t:
                writer.WriteStartObject(); writer.WriteString("type", "text_delta"); writer.WriteString("text", t.Text); writer.WriteEndObject(); break;
            case ContentBlockDelta.InputJsonDelta ij:
                writer.WriteStartObject(); writer.WriteString("type", "input_json_delta"); writer.WriteString("partial_json", ij.PartialJson); writer.WriteEndObject(); break;
            case ContentBlockDelta.ThinkingDelta th:
                writer.WriteStartObject(); writer.WriteString("type", "thinking_delta"); writer.WriteString("thinking", th.Thinking); writer.WriteEndObject(); break;
            case ContentBlockDelta.SignatureDelta s:
                writer.WriteStartObject(); writer.WriteString("type", "signature_delta"); writer.WriteString("signature", s.Signature); writer.WriteEndObject(); break;
            default:
                throw new JsonException($"Unknown ContentBlockDelta type: {value.GetType().Name}");
        }
    }
}
