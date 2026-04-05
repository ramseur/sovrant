using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Memory;

/// <summary>
/// A project convention or coding pattern extracted from high-quality sessions.
/// Examples: "This project uses NUnit, not xUnit", "API routes follow /v1/{resource}/{id}".
/// </summary>
public sealed record LearnedPattern
{
    /// <summary>Stable identifier for deduplication and updates.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The pattern description in plain English.</summary>
    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    /// <summary>Which project this pattern applies to (directory path).</summary>
    [JsonPropertyName("project")]
    public required string Project { get; init; }

    /// <summary>Session ID that first produced this pattern.</summary>
    [JsonPropertyName("source_session")]
    public string? SourceSession { get; init; }

    /// <summary>Confidence score (0.0–1.0). Increases on reinforcement, decreases on correction.</summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; init; } = 0.5;

    /// <summary>When the pattern was first learned.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When the pattern was last matched/used.</summary>
    [JsonPropertyName("last_used")]
    public DateTimeOffset LastUsed { get; init; } = DateTimeOffset.UtcNow;
}
