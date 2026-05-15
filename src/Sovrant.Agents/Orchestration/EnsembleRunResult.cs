using Sovrant.Agents.Swarm;

namespace Sovrant.Agents.Orchestration;

/// <summary>
/// Phase 52 — result from <see cref="IAgentOrchestrator.RunAsync"/>.
/// Wraps the underlying <see cref="SwarmResult"/> with the unified run ID.
/// </summary>
public sealed class EnsembleRunResult
{
    /// <summary>The unified run ID from <c>agent_runs</c>.</summary>
    public required string RunId { get; init; }

    /// <summary>The underlying swarm result (tasks, status, tokens, duration).</summary>
    public required SwarmResult SwarmResult { get; init; }

    /// <summary>Team ID used for the run (null for engine-decided ephemeral runs).</summary>
    public string? TeamId { get; init; }

    /// <summary>Whether ephemeral workers were published as a named team.</summary>
    public string? PublishedTeamId { get; init; }
}
