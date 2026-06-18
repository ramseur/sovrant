using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Memory;

/// <summary>
/// A condensed record of what happened during a single conversation session.
/// Extracted from the JSONL transcript at session end.
/// </summary>
public sealed record SessionSummary
{
    /// <summary>Unique identifier (typically the session ID).</summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>Project directory the session was associated with.</summary>
    [JsonPropertyName("project")]
    public required string Project { get; init; }

    /// <summary>When the session started (first entry timestamp).</summary>
    [JsonPropertyName("started_at")]
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>When the session ended (last entry timestamp).</summary>
    [JsonPropertyName("ended_at")]
    public required DateTimeOffset EndedAt { get; init; }

    /// <summary>Natural-language tasks the user requested (extracted from user messages).</summary>
    [JsonPropertyName("tasks")]
    public IReadOnlyList<string> Tasks { get; init; } = [];

    /// <summary>Distinct tool names used during the session.</summary>
    [JsonPropertyName("tools_used")]
    public IReadOnlyList<string> ToolsUsed { get; init; } = [];

    /// <summary>Files that were modified (paths extracted from tool inputs/outputs).</summary>
    [JsonPropertyName("files_modified")]
    public IReadOnlyList<string> FilesModified { get; init; } = [];

    /// <summary>Whether the session ended successfully.</summary>
    [JsonPropertyName("outcome")]
    public SessionOutcome Outcome { get; init; } = SessionOutcome.Unknown;

    /// <summary>Total input tokens consumed.</summary>
    [JsonPropertyName("total_input_tokens")]
    public int TotalInputTokens { get; init; }

    /// <summary>Total output tokens generated.</summary>
    [JsonPropertyName("total_output_tokens")]
    public int TotalOutputTokens { get; init; }

    /// <summary>Number of user messages in the session.</summary>
    [JsonPropertyName("turn_count")]
    public int TurnCount { get; init; }

    /// <summary>Number of tool errors encountered.</summary>
    [JsonPropertyName("error_count")]
    public int ErrorCount { get; init; }

    /// <summary>User ID of the session owner. Empty string means unowned (legacy / global).</summary>
    [JsonPropertyName("owner_user_id")]
    public string OwnerUserId { get; init; } = string.Empty;
}

/// <summary>How the session ended.</summary>
public enum SessionOutcome
{
    /// <summary>Outcome could not be determined.</summary>
    Unknown,

    /// <summary>Session completed with no tool errors in the final turn.</summary>
    Success,

    /// <summary>Session ended with one or more tool errors in the final turn.</summary>
    Failure,
}
