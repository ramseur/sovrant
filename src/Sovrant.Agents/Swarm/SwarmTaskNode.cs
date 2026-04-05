namespace Sovrant.Agents.Swarm;

/// <summary>Status of a single task within a swarm.</summary>
public enum SwarmTaskStatus
{
    Pending,
    Blocked,
    Running,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>A single node in the decomposed task DAG.</summary>
public sealed class SwarmTaskNode
{
    /// <summary>Unique task identifier (e.g. "task-01").</summary>
    public required string Id { get; init; }

    /// <summary>Natural-language description of what this task does.</summary>
    public required string Description { get; init; }

    /// <summary>IDs of tasks that must complete before this one can start.</summary>
    public IList<string> Dependencies { get; init; } = [];

    /// <summary>Files this task is predicted to modify (for pessimistic locking).</summary>
    public IList<string> FilesToModify { get; init; } = [];

    /// <summary>Agent template name to use for this task (e.g. "coder", "security-reviewer").</summary>
    public string? AgentTemplate { get; init; }

    /// <summary>Optional tool whitelist for the agent.</summary>
    public IList<string>? AllowedTools { get; init; }

    /// <summary>Parallel wave index (0 = no dependencies, computed by topological sort).</summary>
    public int Wave { get; set; }

    /// <summary>Current execution status.</summary>
    public SwarmTaskStatus Status { get; set; } = SwarmTaskStatus.Pending;

    /// <summary>Agent output text on completion.</summary>
    public string? Output { get; set; }

    /// <summary>Error message on failure.</summary>
    public string? Error { get; set; }

    /// <summary>Total tokens consumed by this task (input + output).</summary>
    public int TokensUsed { get; set; }

    /// <summary>Number of retry attempts so far.</summary>
    public int RetryCount { get; set; }
}
