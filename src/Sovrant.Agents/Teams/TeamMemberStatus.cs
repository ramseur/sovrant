namespace Sovrant.Agents.Teams;

/// <summary>Lifecycle status of a team member agent.</summary>
public enum TeamMemberStatus
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled,
}
