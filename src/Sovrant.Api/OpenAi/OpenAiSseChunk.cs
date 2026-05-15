using System.Text.Json.Serialization;

namespace Sovrant.Api.OpenAi;

internal sealed record OpenAiSseChunk(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiSseChoice>? Choices)
{
    [JsonPropertyName("usage")]
    public OpenAiUsage? Usage { get; init; }
}

internal sealed record OpenAiSseChoice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("delta")] OpenAiSseDelta Delta,
    [property: JsonPropertyName("finish_reason")] string? FinishReason);

internal sealed record OpenAiSseDelta(
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("content")] string? Content)
{
    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<OpenAiSseToolCallDelta>? ToolCalls { get; init; }
}

internal sealed record OpenAiSseToolCallDelta(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("function")] OpenAiSseFunctionDelta? Function);

internal sealed record OpenAiSseFunctionDelta(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("arguments")] string? Arguments);
