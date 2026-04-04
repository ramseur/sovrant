using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Sovrant.Server.Webhooks;

/// <summary>
/// Response payload returned from the webhook endpoint (synchronous mode)
/// or POSTed to <see cref="WebhookRequest.CallbackUrl"/> (async mode).
/// </summary>
public sealed class WebhookResponse
{
    /// <summary>Whether the agent completed without errors.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>Source identifier echoed from the request.</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>External user identifier echoed from the request.</summary>
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Thread/channel ID echoed from the request.</summary>
    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }

    /// <summary>The Sovrant session ID used for this conversation.</summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The agent's text response.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Tool invocations that occurred during the turn.</summary>
    [JsonPropertyName("tool_calls")]
    public Collection<WebhookToolCall> ToolCalls { get; } = [];

    /// <summary>Errors encountered during the turn.</summary>
    [JsonPropertyName("errors")]
    public Collection<string> Errors { get; } = [];

    /// <summary>Total input tokens consumed.</summary>
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    /// <summary>Total output tokens generated.</summary>
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

/// <summary>A tool invocation that occurred during the webhook turn.</summary>
public sealed class WebhookToolCall
{
    /// <summary>Tool use identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Tool name.</summary>
    [JsonPropertyName("tool_name")]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Whether the tool returned an error.</summary>
    [JsonPropertyName("is_error")]
    public bool IsError { get; set; }
}
