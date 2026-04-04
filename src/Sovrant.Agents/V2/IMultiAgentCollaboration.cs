namespace Sovrant.Agents.V2;

/// <summary>
/// [V2 Placeholder — not yet implemented. See Phase 19 roadmap.]
/// <para>
/// Orchestrates a structured collaboration pattern across a named set of agents —
/// for example a plan → code → review cycle — using a shared <see cref="ITeamWorkspace"/>.
/// Agents communicate via typed messages rather than free-form prompt injection.
/// No networking or external dependencies are required.
/// </para>
/// </summary>
public interface IMultiAgentCollaboration
{
    /// <summary>
    /// Gets the name of this collaboration pattern, e.g. <c>"PlanCodeReview"</c>
    /// or <c>"ResearchAndSummarise"</c>.
    /// </summary>
    string Name { get; }

    /// <summary>The workspace shared by all participating agents in this collaboration.</summary>
    ITeamWorkspace Workspace { get; }

    /// <summary>
    /// Runs the full collaboration to completion and returns the final synthesised output.
    /// Each agent is invoked in the sequence defined by the collaboration pattern.
    /// </summary>
    /// <param name="goal">The high-level goal to achieve, expressed in natural language.</param>
    /// <param name="ct">Cancellation token; cancels the entire collaboration.</param>
    Task<string> RunAsync(string goal, CancellationToken ct = default);
}
