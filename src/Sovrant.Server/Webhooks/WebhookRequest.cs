using System.Text.Json.Serialization;

namespace Sovrant.Server.Webhooks;

/// <summary>Incoming webhook request from Slack, Teams, Discord, or any HTTP source.</summary>
public sealed class WebhookRequest
{
    /// <summary>Source identifier (e.g. "slack", "teams", "discord", "custom").</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// External user identifier. Combined with <see cref="Source"/> to derive a stable
    /// Sovrant session ID: <c>{source}:{user_id}</c>.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>The user message to process.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional URL where the result will be POSTed. When null, the response is returned
    /// synchronously in the HTTP response body.
    /// </summary>
    [JsonPropertyName("callback_url")]
    public string? CallbackUrl { get; set; }

    /// <summary>Optional model override for this request.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Optional thread/channel ID for context (informational, included in callback).</summary>
    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; set; }
}
