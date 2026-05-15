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
    /// Extension field: per-session MCP connection allow-list. Names of MCP
    /// servers whose tools should be exposed for this turn (and persisted as
    /// the session's gate). <c>null</c> means leave the existing gate unchanged;
    /// an empty list disables all MCP tools.
    /// </summary>
    [JsonPropertyName("mcp_connections")]
    public IReadOnlyList<string>? McpConnections { get; init; }
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
