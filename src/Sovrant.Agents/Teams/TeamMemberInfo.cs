using Sovrant.Agents.Models;

namespace Sovrant.Agents.Teams;

/// <summary>
/// Describes a member of a multi-agent team. Tracks identity, configuration,
/// and mutable lifecycle state.
/// </summary>
public sealed class TeamMemberInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required AgentRole Role { get; init; }
    public required string SystemPrompt { get; init; }
    public IReadOnlyList<string>? AllowedTools { get; init; }
    public string? Model { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Mutable lifecycle state
    public TeamMemberStatus Status { get; set; } = TeamMemberStatus.Idle;
    public string? LastOutput { get; set; }
    public string? LastError { get; set; }
}
