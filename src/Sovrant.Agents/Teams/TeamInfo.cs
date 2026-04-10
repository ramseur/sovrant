namespace Sovrant.Agents.Teams;

/// <summary>
/// Describes a named team of agent members, scoped to a workspace and
/// optionally a project. Persisted in the <c>teams</c> table.
/// </summary>
public sealed record TeamInfo(
    string Id,
    string WorkspaceId,
    string? ProjectId,
    string Name,
    string? Description,
    string Origin,
    string CreatedBy,
    DateTimeOffset CreatedAt);
