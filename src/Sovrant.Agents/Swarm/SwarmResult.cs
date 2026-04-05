namespace Sovrant.Agents.Swarm;

/// <summary>Overall status of a swarm execution.</summary>
public enum SwarmStatus
{
    Planning,
    Executing,
    QualityReview,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>Quality gate verdict from post-execution review.</summary>
public sealed record QualityVerdict(int Score, string Verdict, string Feedback);

/// <summary>Final result of a swarm execution.</summary>
public sealed class SwarmResult
{
    /// <summary>Swarm session identifier.</summary>
    public required string SwarmId { get; init; }

    /// <summary>Overall swarm status.</summary>
    public required SwarmStatus Status { get; set; }

    /// <summary>All tasks with their final states.</summary>
    public required IList<SwarmTaskNode> Tasks { get; init; }

    /// <summary>Total tokens consumed across all tasks.</summary>
    public int TotalTokensUsed { get; set; }

    /// <summary>Total execution duration.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Quality gate result (null if gate not run).</summary>
    public QualityVerdict? QualityGate { get; set; }

    /// <summary>Combined output from all completed tasks.</summary>
    public string CombinedOutput { get; set; } = string.Empty;
}
