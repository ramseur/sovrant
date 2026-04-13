using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Metrics;

/// <summary>
/// A cached snapshot of model pricing data, with metadata about when it was fetched.
/// Serialised to disk as the fallback cache.
/// </summary>
public sealed record PricingSnapshot
{
    [JsonPropertyName("fetched_at")]
    public required DateTimeOffset FetchedAt { get; init; }

    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("models")]
    public required IReadOnlyDictionary<string, ModelPricing> Models { get; init; }
}
