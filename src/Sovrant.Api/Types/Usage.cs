using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>Token usage information from the API.</summary>
public sealed record Usage(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("cache_creation_input_tokens")] int CacheCreationInputTokens = 0,
    [property: JsonPropertyName("cache_read_input_tokens")] int CacheReadInputTokens = 0,
    [property: JsonPropertyName("output_tokens")] int OutputTokens = 0)
{
    /// <summary>Gets the total number of tokens used.</summary>
    public int TotalTokens => InputTokens + CacheCreationInputTokens + CacheReadInputTokens + OutputTokens;
}
