using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Metrics;

/// <summary>
/// A single cost record written to the JSONL metrics log.
/// One record per completed LLM turn.
/// </summary>
public sealed record CostTurnRecord
{
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input_tokens")]
    public required long InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public required long OutputTokens { get; init; }

    [JsonPropertyName("estimated_usd")]
    public decimal? EstimatedUsd { get; init; }

    [JsonPropertyName("actual_usd")]
    public decimal? ActualUsd { get; init; }

    /// <summary>
    /// Where the pricing data came from: "openrouter-live", "openrouter-disk-cache", "openrouter-memory-cache", "none".
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// OpenRouter generation id, if the request was routed through OpenRouter.
    /// Used for actual-cost reconciliation via <c>GET /api/v1/generation/:id</c>.
    /// </summary>
    [JsonPropertyName("generation_id")]
    public string? GenerationId { get; init; }
}
