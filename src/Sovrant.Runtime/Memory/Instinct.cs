using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Memory;

/// <summary>
/// A long-term behavioral rule with confidence scoring and evidence tracking.
/// Confidence increases on positive reinforcement and decreases on correction.
/// Instincts below a confidence threshold (default 0.3) are pruned.
/// </summary>
public sealed record Instinct
{
    /// <summary>Stable identifier (e.g. "instinct-001").</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The situation or signal that activates this instinct.</summary>
    [JsonPropertyName("trigger")]
    public required string Trigger { get; init; }

    /// <summary>What to do when the trigger matches.</summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    /// <summary>Confidence score (0.0–1.0). Below <see cref="PruneThreshold"/> the instinct is removed.</summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; init; } = 0.5;

    /// <summary>Timestamped evidence entries that support or weaken this instinct.</summary>
    [JsonPropertyName("evidence")]
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>When the instinct was first created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When the instinct was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>User ID of the session that created this instinct. Empty string means unowned (legacy / global).</summary>
    [JsonPropertyName("owner_user_id")]
    public string OwnerUserId { get; init; } = string.Empty;

    /// <summary>Default confidence threshold below which instincts are pruned.</summary>
    public const double PruneThreshold = 0.3;

    /// <summary>Amount to add to confidence on positive reinforcement.</summary>
    public const double ReinforcementDelta = 0.1;

    /// <summary>Amount to subtract from confidence on correction.</summary>
    public const double CorrectionDelta = 0.15;
}
