using System.Text.Json.Serialization;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Projects;

/// <summary>An isolated container within a workspace that groups related work.</summary>
public sealed record Project
{
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("workspace_id")]
    public required string WorkspaceId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("archived_at")]
    public DateTimeOffset? ArchivedAt { get; init; }
}

/// <summary>A member of a project (subset of workspace members).</summary>
public sealed record ProjectMember
{
    [JsonPropertyName("project_id")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("role")]
    public required ProjectRole Role { get; init; }

    [JsonPropertyName("joined_at")]
    public DateTimeOffset JoinedAt { get; init; }
}

/// <summary>Role within a project.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectRole>))]
public enum ProjectRole
{
    Lead,
    Contributor,
    Viewer,
}

/// <summary>Aggregated token usage for a project.</summary>
public sealed record ProjectUsage
{
    [JsonPropertyName("total_input_tokens")]
    public long TotalInputTokens { get; init; }

    [JsonPropertyName("total_output_tokens")]
    public long TotalOutputTokens { get; init; }

    [JsonPropertyName("total_cost_usd")]
    public double TotalCostUsd { get; init; }

    [JsonPropertyName("session_count")]
    public int SessionCount { get; init; }
}
