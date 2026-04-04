using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Api.OpenAi;

/// <summary>Request body for the OpenAI Responses API (<c>POST /v1/responses</c>).</summary>
internal sealed record OpenAiResponsesRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input")] IReadOnlyList<OpenAiResponsesInputItem> Input)
{
    [JsonPropertyName("instructions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instructions { get; init; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<OpenAiResponsesTool>? Tools { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("max_output_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOutputTokens { get; init; }
}

/// <summary>
/// A single item in the Responses API <c>input</c> array.
/// Covers user/assistant messages, function calls (from history), and function call outputs.
/// </summary>
internal sealed record OpenAiResponsesInputItem(
    [property: JsonPropertyName("type")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Type,
    [property: JsonPropertyName("role")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Role,
    [property: JsonPropertyName("content")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Content,
    [property: JsonPropertyName("id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Id,
    [property: JsonPropertyName("call_id")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CallId,
    [property: JsonPropertyName("name")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Name,
    [property: JsonPropertyName("arguments")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Arguments,
    [property: JsonPropertyName("output")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Output);

/// <summary>Tool entry for the Responses API — covers built-in tools and function tools.</summary>
internal sealed record OpenAiResponsesTool(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Name = null,
    [property: JsonPropertyName("description")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Description = null,
    [property: JsonPropertyName("parameters")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Parameters = null);

/// <summary>Non-streaming response from the Responses API.</summary>
internal sealed record OpenAiResponsesResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("output")] IReadOnlyList<OpenAiResponsesOutputItem>? Output,
    [property: JsonPropertyName("usage")] OpenAiResponsesUsage? Usage);

/// <summary>A single output item in a Responses API response (message, function_call, web_search_call, etc.).</summary>
internal sealed record OpenAiResponsesOutputItem(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("call_id")] string? CallId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("arguments")] string? Arguments,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("content")] IReadOnlyList<OpenAiResponsesContentPart>? Content,
    [property: JsonPropertyName("status")] string? Status);

/// <summary>A content part within a message output item.</summary>
internal sealed record OpenAiResponsesContentPart(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] string? Text);

/// <summary>Token usage counters in a Responses API response.</summary>
internal sealed record OpenAiResponsesUsage(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens);

// ── SSE chunk types ────────────────────────────────────────────────────────────

/// <summary>Data for <c>response.output_text.delta</c> SSE events.</summary>
internal sealed record OpenAiResponsesTextDeltaChunk(
    [property: JsonPropertyName("output_index")] int OutputIndex,
    [property: JsonPropertyName("content_index")] int ContentIndex,
    [property: JsonPropertyName("delta")] string? Delta);

/// <summary>Data for <c>response.output_item.added</c> and <c>response.output_item.done</c> SSE events.</summary>
internal sealed record OpenAiResponsesOutputItemChunk(
    [property: JsonPropertyName("output_index")] int OutputIndex,
    [property: JsonPropertyName("item")] OpenAiResponsesOutputItem? Item);

/// <summary>Data for <c>response.function_call_arguments.delta</c> SSE events.</summary>
internal sealed record OpenAiResponsesFunctionCallDeltaChunk(
    [property: JsonPropertyName("output_index")] int OutputIndex,
    [property: JsonPropertyName("delta")] string? Delta);

/// <summary>Data for <c>response.completed</c> SSE events.</summary>
internal sealed record OpenAiResponsesCompletedChunk(
    [property: JsonPropertyName("response")] OpenAiResponsesResponse? Response);
