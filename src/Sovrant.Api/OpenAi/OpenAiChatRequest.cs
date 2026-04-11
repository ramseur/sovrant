using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Api.OpenAi;

/// <summary>OpenAI-format chat completions request (internal conversion use only).</summary>
internal sealed record OpenAiChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiMessage> Messages)
{
    /// <summary>Legacy max_tokens parameter — used by most OpenAI-compat providers.</summary>
    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }

    /// <summary>Newer max_completion_tokens parameter — required by OpenAI reasoning models (o1, o3, o4).</summary>
    [JsonPropertyName("max_completion_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxCompletionTokens { get; init; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<OpenAiTool>? Tools { get; init; }

    /// <summary>
    /// When tools are present, set to "auto" so the model can decide whether to call a tool.
    /// Omitted when no tools are defined.
    /// </summary>
    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolChoice { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("stream_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiStreamOptions? StreamOptions { get; init; }
}

internal sealed record OpenAiMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string? Content)
{
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<OpenAiToolCall>? ToolCalls { get; init; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }
}

internal sealed record OpenAiToolCall(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] OpenAiFunction Function);

internal sealed record OpenAiFunction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] string Arguments);

internal sealed record OpenAiTool(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OpenAiToolFunction? Function);

internal sealed record OpenAiToolFunction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("parameters")] JsonElement Parameters);

internal sealed record OpenAiStreamOptions(
    [property: JsonPropertyName("include_usage")] bool IncludeUsage);
