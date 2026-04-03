using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>A request to the LLM Messages API (Anthropic-compatible format).</summary>
public sealed record MessagesRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("messages")] IReadOnlyList<InputMessage> Messages)
{
    /// <summary>An optional system prompt.</summary>
    [JsonPropertyName("system")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? System { get; init; }

    /// <summary>The tools available to the model.</summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }

    /// <summary>Controls how the model chooses which tool to use.</summary>
    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolChoice? ToolChoice { get; init; }

    /// <summary>Whether to stream the response using SSE.</summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; init; }
}
