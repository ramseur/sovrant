namespace Sovrant.Agents.Models;

/// <summary>Describes the functional role of an agent within a multi-agent team.</summary>
public enum AgentRole
{
    /// <summary>No specific role. Agent handles general-purpose tasks.</summary>
    General,

    /// <summary>Decomposes a high-level goal into sub-tasks for other agents.</summary>
    Planner,

    /// <summary>Writes and modifies source code.</summary>
    Coder,

    /// <summary>Reviews code or plans produced by other agents.</summary>
    Reviewer,

    /// <summary>Executes shell commands and performs file-system operations.</summary>
    Executor,

    /// <summary>Coordinates and monitors other agents; synthesises their results.</summary>
    Supervisor,

    /// <summary>Writes and runs tests to validate changes.</summary>
    Tester,

    /// <summary>Researches information, reads documentation, and gathers context.</summary>
    Researcher,
}
