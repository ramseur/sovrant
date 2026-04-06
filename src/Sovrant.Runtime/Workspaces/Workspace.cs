using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Workspaces;

/// <summary>A workspace that groups projects, sessions, memory, and configuration.</summary>
public sealed record Workspace
{
    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    [JsonPropertyName("type")]
    public required WorkspaceType Type { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("owner_id")]
    public required string OwnerId { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Type of workspace.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkspaceType>))]
public enum WorkspaceType
{
    /// <summary>Auto-created per user, single-owner, cannot be deleted.</summary>
    Personal,

    /// <summary>Explicitly created, supports membership and invites.</summary>
    Team,
}

/// <summary>A member of a workspace.</summary>
public sealed record WorkspaceMember
{
    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("role")]
    public required WorkspaceRole Role { get; init; }

    [JsonPropertyName("joined_at")]
    public DateTimeOffset JoinedAt { get; init; }
}

/// <summary>Membership role within a workspace.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkspaceRole>))]
public enum WorkspaceRole
{
    Owner,
    Admin,
    Member,
    Viewer,
}

/// <summary>An invite to join a workspace.</summary>
public sealed record WorkspaceInvite
{
    [JsonPropertyName("invite_id")]
    public required string InviteId { get; init; }

    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("role")]
    public required WorkspaceRole Role { get; init; }

    [JsonPropertyName("token")]
    public required string Token { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("accepted_at")]
    public DateTimeOffset? AcceptedAt { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>A memory entry scoped to a workspace.</summary>
public sealed record WorkspaceMemoryEntry
{
    [JsonPropertyName("memory_id")]
    public required string MemoryId { get; init; }

    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    [JsonPropertyName("layer")]
    public required string Layer { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; init; }

    [JsonPropertyName("project_id")]
    public string? ProjectId { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }
}
