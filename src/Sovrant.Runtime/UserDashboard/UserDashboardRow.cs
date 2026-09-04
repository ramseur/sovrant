namespace Sovrant.Runtime.UserDashboard;

/// <summary>
/// Phase 98 — one flat row in the User Dashboard grid.
/// Same shape as <see cref="Sovrant.Runtime.CommandCenter.CommandCenterRow"/>
/// plus two per-user flags: <see cref="IsOwn"/> (does the current user own this
/// record) and <see cref="IsPrivate"/> (was the record marked private — only
/// ever set when <see cref="IsOwn"/> is true, since other users' private rows
/// are excluded entirely upstream by the aggregator).
/// </summary>
public sealed record UserDashboardRow(
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
    string? ProjectId = null,
    bool IsOwn = false,
    bool IsPrivate = false);

/// <summary>The aggregated User Dashboard state at a single point in time.</summary>
public sealed record UserDashboardState(
    DateTimeOffset GeneratedAt,
    int OwnWorkflows,
    int OwnTeamRuns,
    int OwnAgentRuns,
    int OwnSessions,
    int OthersPublicRows,
    int Claws,
    IReadOnlyList<UserDashboardRow> Rows);
