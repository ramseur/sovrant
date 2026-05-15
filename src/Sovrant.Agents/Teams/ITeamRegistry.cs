namespace Sovrant.Agents.Teams;

/// <summary>Registry for managing teams and their member agents.</summary>
public interface ITeamRegistry
{
    // ── Member-level (original Phase 19 API) ────────────────────────────

    /// <summary>Registers a new team member and returns its ID.</summary>
    string RegisterMember(TeamMemberInfo member);

    /// <summary>Removes a team member by ID. Returns true if found and removed.</summary>
    bool RemoveMember(string memberId);

    /// <summary>Gets a team member by ID, or null if not found.</summary>
    TeamMemberInfo? GetMember(string memberId);

    /// <summary>Returns all registered team members.</summary>
    IReadOnlyList<TeamMemberInfo> GetAllMembers();

    // ── Team-level (Phase 52) ───────────────────────────────────────────

    /// <summary>Creates a new team. Returns the team ID.</summary>
    string CreateTeam(TeamInfo team) => throw new NotSupportedException();

    /// <summary>Gets a team by ID, or null if not found.</summary>
    TeamInfo? GetTeam(string teamId) => null;

    /// <summary>Lists teams, optionally filtered by workspace.</summary>
    IReadOnlyList<TeamInfo> ListTeams(string? workspaceId = null) => [];

    /// <summary>Removes a team and all its members. Returns true if found and removed.</summary>
    bool RemoveTeam(string teamId) => false;

    /// <summary>Returns members belonging to a specific team.</summary>
    IReadOnlyList<TeamMemberInfo> GetTeamMembers(string teamId) => [];

    /// <summary>Updates the run profile (mode, concurrency, safeguards) for a team. Returns true if found and updated.</summary>
    bool UpdateTeamRunProfile(string teamId, TeamRunProfile profile) => false;
}
