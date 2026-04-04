using System.Text.Json.Serialization;

namespace Sovrant.Server.OpenAi;

/// <summary>An OpenAI-compatible streaming chunk.</summary>
public sealed class ChatCompletionChunk
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; init; } = "chat.completion.chunk";

    [JsonPropertyName("created")]
    public long Created { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public IReadOnlyList<ChunkChoice> Choices { get; init; } = [];

    /// <summary>
    /// Extension field: Sovrant-specific event metadata (tool use, tool result).
    /// Ignored by standard OpenAI clients; read by the Sovrant frontend.
    /// </summary>
    [JsonPropertyName("sovrant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SovrantEvent? Sovrant { get; init; }

    /// <summary>Token usage — only populated on the final chunk.</summary>
    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UsageInfo? Usage { get; init; }
}

/// <summary>A single choice in a streaming chunk.</summary>
public sealed class ChunkChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("delta")]
    public DeltaContent Delta { get; init; } = new();

    [JsonPropertyName("finish_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FinishReason { get; init; }
}

/// <summary>The incremental content delta within a streaming chunk.</summary>
public sealed class DeltaContent
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }
}

/// <summary>Sovrant extension event payload — included alongside OpenAI chunks for tool visibility.</summary>
public sealed class SovrantEvent
{
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    [JsonPropertyName("tool_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolName { get; init; }

    [JsonPropertyName("tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolUseId { get; init; }

    [JsonPropertyName("is_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsError { get; init; }
}

/// <summary>Token usage summary on the final chunk.</summary>
public sealed class UsageInfo
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}

/// <summary>A non-streaming chat completion response.</summary>
public sealed class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; init; } = "chat.completion";

    [JsonPropertyName("created")]
    public long Created { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public IReadOnlyList<ResponseChoice> Choices { get; init; } = [];

    [JsonPropertyName("usage")]
    public UsageInfo Usage { get; init; } = new();
}

/// <summary>A single choice in a non-streaming response.</summary>
public sealed class ResponseChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("message")]
    public ResponseMessage Message { get; init; } = new();

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; init; } = "stop";
}

/// <summary>A complete message in a non-streaming response.</summary>
public sealed class ResponseMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "assistant";

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}
