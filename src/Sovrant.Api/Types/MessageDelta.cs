using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>A delta update for a message during streaming.</summary>
public sealed record MessageDelta(
    [property: JsonPropertyName("stop_reason")] string? StopReason,
    [property: JsonPropertyName("stop_sequence")] string? StopSequence);
