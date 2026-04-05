namespace Sovrant.Agents.Teams;

/// <summary>Registry for managing team member agents.</summary>
public interface ITeamRegistry
{
    /// <summary>Registers a new team member and returns its ID.</summary>
    string RegisterMember(TeamMemberInfo member);

    /// <summary>Removes a team member by ID. Returns true if found and removed.</summary>
    bool RemoveMember(string memberId);

    /// <summary>Gets a team member by ID, or null if not found.</summary>
    TeamMemberInfo? GetMember(string memberId);

    /// <summary>Returns all registered team members.</summary>
    IReadOnlyList<TeamMemberInfo> GetAllMembers();
}
