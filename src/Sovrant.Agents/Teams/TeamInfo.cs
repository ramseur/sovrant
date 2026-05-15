namespace Sovrant.Agents.Teams;

/// <summary>How a team runs: one-at-a-time, or in parallel (with or without swarm safeguards).</summary>
public enum TeamRunMode
{
    Sequential,
    Parallel,
    Swarm,
}

/// <summary>Controls whether and how the team decomposes a prompt into a multi-task plan.</summary>
public enum TeamDecompositionMode
{
    Off,
    RoleAware,
    Open,
}

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
    DateTimeOffset CreatedAt)
{
    /// <summary>Execution profile for this team's runs.</summary>
    /// <remarks>
    /// Phase 78 Path 2 commit 7 — new teams default to <see cref="TeamRunMode.Parallel"/>
    /// so picking "Team" in the UI isn't a throughput downgrade vs. Swarm. Existing
    /// persisted teams keep whatever was written at V015 migration time (sequential
    /// for pre-V015 rows). Sequential remains available as an explicit opt-in.
    /// </remarks>
    public TeamRunMode RunMode { get; init; } = TeamRunMode.Parallel;

    /// <summary>Upper bound on concurrent member execution. Ignored when <see cref="RunMode"/> is <see cref="TeamRunMode.Sequential"/>.</summary>
    public int MaxConcurrent { get; init; } = 4;

    /// <summary>When true, parallel member tasks acquire per-file locks to serialize writes.</summary>
    public bool FileLocksEnabled { get; init; }

    /// <summary>When true, a quality-gate reviewer scores the combined output and can trigger one retry.</summary>
    public bool QualityGateEnabled { get; init; }

    /// <summary>Quality score (0–10) at or above which the run is accepted. Default 7.</summary>
    public int QualityGateThreshold { get; init; } = 7;

    /// <summary>How aggressively to split a prompt into sub-tasks before dispatching to members.</summary>
    public TeamDecompositionMode DecompositionMode { get; init; } = TeamDecompositionMode.Off;
}

/// <summary>
/// Mutable run-profile fields for a team, used by <see cref="ITeamRegistry.UpdateTeamRunProfile"/>.
/// Allows the UI and API to update run behavior without rewriting the core identity fields.
/// </summary>
public sealed record TeamRunProfile(
    TeamRunMode RunMode,
    int MaxConcurrent,
    bool FileLocksEnabled,
    bool QualityGateEnabled,
    int QualityGateThreshold,
    TeamDecompositionMode DecompositionMode);
