using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Users;

/// <summary>
/// A user account. Phase 37 — user management API surface over the Phase 32
/// <c>users</c> table. After V043 the <c>user_id</c> equals the user's email
/// for auth-registered users; OS-seeded identities keep their existing value.
/// </summary>
public sealed record User
{
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("team")]
    public string? Team { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Profile view returned by <c>GET /v1/users/{id}</c> — extends <see cref="User"/> with derived stats.</summary>
public sealed record UserProfile
{
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("team")]
    public string? Team { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("session_count")]
    public int SessionCount { get; init; }

    [JsonPropertyName("total_input_tokens")]
    public long TotalInputTokens { get; init; }

    [JsonPropertyName("total_output_tokens")]
    public long TotalOutputTokens { get; init; }

    [JsonPropertyName("total_cost_usd")]
    public double TotalCostUsd { get; init; }

    /// <summary>Derived from <c>MAX(updated_at)</c> on the user's sessions; <c>null</c> if the user has no sessions.</summary>
    [JsonPropertyName("last_seen_at")]
    public DateTimeOffset? LastSeenAt { get; init; }
}

/// <summary>Aggregated token usage for a user.</summary>
public sealed record UserUsage
{
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("total_input_tokens")]
    public long TotalInputTokens { get; init; }

    [JsonPropertyName("total_output_tokens")]
    public long TotalOutputTokens { get; init; }

    [JsonPropertyName("total_cost_usd")]
    public double TotalCostUsd { get; init; }

    [JsonPropertyName("session_count")]
    public int SessionCount { get; init; }

    /// <summary>Per-model breakdown.</summary>
    [JsonPropertyName("by_model")]
    public IReadOnlyList<UserUsageByModel> ByModel { get; init; } = Array.Empty<UserUsageByModel>();
}

/// <summary>Per-model usage row.</summary>
public sealed record UserUsageByModel
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input_tokens")]
    public long InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public long OutputTokens { get; init; }

    [JsonPropertyName("cost_usd")]
    public double CostUsd { get; init; }
}

/// <summary>An audit event attributed to a user (joined from sessions → audit_governance).</summary>
public sealed record UserAuditEvent
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    [JsonPropertyName("workspace_id")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("project_id")]
    public string? ProjectId { get; init; }

    [JsonPropertyName("phase")]
    public required string Phase { get; init; }

    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    [JsonPropertyName("action")]
    public required string Action { get; init; }

    [JsonPropertyName("rule")]
    public required string Rule { get; init; }

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}

/// <summary>Filter for <see cref="IUserService.ListAsync"/>.</summary>
public sealed record UserListFilter
{
    public string? Status { get; init; }

    public string? Role { get; init; }

    public string? Team { get; init; }

    public int Limit { get; init; } = 100;

    public int Offset { get; init; }
}
