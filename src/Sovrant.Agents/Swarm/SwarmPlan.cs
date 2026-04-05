namespace Sovrant.Agents.Swarm;

/// <summary>A decomposed task DAG produced by <see cref="ISwarmDecomposer"/>.</summary>
public sealed class SwarmPlan
{
    /// <summary>Unique swarm session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>The original user prompt that was decomposed.</summary>
    public required string OriginalPrompt { get; init; }

    /// <summary>All tasks in the DAG.</summary>
    public required IList<SwarmTaskNode> Tasks { get; init; }

    /// <summary>Total number of parallel waves (computed from topological sort).</summary>
    public int WaveCount { get; init; }

    /// <summary>
    /// Optional team ID whose members should be used as swarm workers.
    /// When set, the orchestrator resolves agents from this team before falling back to templates.
    /// Different orchestrations can reference different teams.
    /// </summary>
    public string? TeamId { get; set; }

    /// <summary>When the plan was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Returns all tasks in the given wave.</summary>
    public IReadOnlyList<SwarmTaskNode> GetWave(int wave) =>
        Tasks.Where(t => t.Wave == wave).ToList();
}
