using System.Text.Json.Serialization;

namespace Sovrant.Server.OpenAi;

/// <summary>An OpenAI-compatible chat completions request.</summary>
public sealed class ChatCompletionRequest
{
    /// <summary>The model to use for this request. Overrides the server default for this call only.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>The conversation messages.</summary>
    [JsonPropertyName("messages")]
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    /// <summary>Whether to stream the response as SSE chunks.</summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = true;

    /// <summary>Maximum tokens to generate.</summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Extension field: an existing session ID to resume.
    /// If provided, the server loads the session history before running the turn.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    /// Extension field: per-request LLM API key. Overrides the server's global key for this call only.
    /// The server never logs, persists, or includes this value in error responses.
    /// </summary>
    [JsonPropertyName("x_api_key")]
    public string? XApiKey { get; init; }

    /// <summary>
    /// Extension field: per-request LLM base URL. Overrides the server's global base URL for this call only.
    /// Must include protocol (e.g. <c>https://api.openai.com/v1</c>).
    /// </summary>
    [JsonPropertyName("x_base_url")]
    public string? XBaseUrl { get; init; }
}

/// <summary>A single message in a chat completion request.</summary>
public sealed class ChatMessage
{
    /// <summary>The role of the message author (<c>user</c>, <c>assistant</c>, or <c>system</c>).</summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = "user";

    /// <summary>The text content of the message.</summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}
