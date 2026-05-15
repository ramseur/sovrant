namespace Sovrant.Runtime.Metrics;

/// <summary>
/// Optional hints that refine a cost estimate beyond simple input/output token counts.
/// All fields are optional — omitted fields are treated as zero.
/// </summary>
public sealed record CostHints
{
    /// <summary>Tokens read from a cached prompt prefix (Anthropic prompt caching, etc.).</summary>
    public long CacheReadTokens { get; init; }

    /// <summary>Tokens written into a new cache entry.</summary>
    public long CacheWriteTokens { get; init; }

    /// <summary>Number of images included in the prompt.</summary>
    public int ImageCount { get; init; }

    /// <summary>Tokens used for internal reasoning / chain-of-thought (billed separately by some providers).</summary>
    public long InternalReasoningTokens { get; init; }

    /// <summary>Whether the turn included a web search (billed as a flat fee by some models).</summary>
    public bool IncludesWebSearch { get; init; }
}
