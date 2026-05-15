using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Metrics;

/// <summary>
/// Pricing data for a single model, parsed from the OpenRouter <c>/api/v1/models</c> response.
/// All values are USD per unit.
/// </summary>
public sealed record ModelPricingEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("pricing")]
    public ModelPricing? Pricing { get; init; }
}

/// <summary>
/// Per-unit pricing from OpenRouter's model catalogue.
/// String values because OpenRouter returns them as strings (high-precision decimals).
/// </summary>
public sealed record ModelPricing
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    [JsonPropertyName("completion")]
    public string? Completion { get; init; }

    [JsonPropertyName("request")]
    public string? Request { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("web_search")]
    public string? WebSearch { get; init; }

    [JsonPropertyName("internal_reasoning")]
    public string? InternalReasoning { get; init; }

    [JsonPropertyName("input_cache_read")]
    public string? InputCacheRead { get; init; }

    [JsonPropertyName("input_cache_write")]
    public string? InputCacheWrite { get; init; }
}
