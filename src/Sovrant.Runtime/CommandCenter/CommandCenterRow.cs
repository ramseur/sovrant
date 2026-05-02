namespace Sovrant.Runtime.CommandCenter;

/// <summary>
/// Phase 90 / Phase 89 MVP — one flat row in the Command Center grid.
/// Represents one active item across the four sources (mission, team-run,
/// agent-run, session) so the cockpit can render them in a single list.
/// </summary>
public sealed record CommandCenterRow(
    string Kind,
    string Id,
    string Title,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset LastActivity,
    string? OwnerLabel,
    string? Preview,
    decimal? CostUsd,
    string? DetailRoute,
    string? WorkspaceId = null,
    string? ProjectId = null);

/// <summary>The aggregated cockpit state at a single point in time.</summary>
public sealed record CommandCenterState(
    DateTimeOffset GeneratedAt,
    int ActiveMissions,
    int ActiveTeamRuns,
    int ActiveAgentRuns,
    int ActiveSessions,
    IReadOnlyList<CommandCenterRow> Rows);
