namespace Sovrant.Agents.Swarm;

/// <summary>Events emitted during swarm execution for JSONL recording, SSE streaming, and status tracking.</summary>
public abstract record SwarmEvent(string SwarmId, DateTimeOffset Timestamp)
{
    /// <summary>The decomposed plan has been created.</summary>
    public sealed record PlanCreated(string SwarmId, int TaskCount, int WaveCount)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>A task has started execution.</summary>
    public sealed record TaskStarted(string SwarmId, string TaskId, string AgentName, string TaskDescription, int Wave)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>A task completed successfully.</summary>
    public sealed record TaskCompleted(string SwarmId, string TaskId, string AgentName, string TaskDescription, string Output, int TokensUsed)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>A task failed.</summary>
    public sealed record TaskFailed(string SwarmId, string TaskId, string AgentName, string TaskDescription, string Error, int RetryCount)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>A file conflict was detected (task blocked by another task's lock).</summary>
    public sealed record FileConflict(string SwarmId, string TaskId, string AgentName, string FilePath, string HeldByTaskId, string HeldByAgentName)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>Token budget has been exceeded.</summary>
    public sealed record BudgetExceeded(string SwarmId, int Used, int Limit)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>Quality gate review has started.</summary>
    public sealed record QualityGateStarted(string SwarmId)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>Quality gate review completed with a verdict.</summary>
    public sealed record QualityGateCompleted(string SwarmId, QualityVerdict Verdict)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>The swarm has completed (successfully or with failures).</summary>
    public sealed record SwarmCompleted(string SwarmId, SwarmStatus FinalStatus, int TotalTokens, double DurationSeconds)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>Phase 57 — a wave of tasks has completed execution.</summary>
    public sealed record WaveCompleted(string SwarmId, int Wave, int CompletedTasks, int FailedTasks)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>Phase 57 — a coordination event was received from another group.</summary>
    public sealed record CoordinationReceived(string SwarmId, string SourceGroupId, string EventType, string Summary)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);

    /// <summary>Phase 57 — a coordination event was sent to another group.</summary>
    public sealed record CoordinationSent(string SwarmId, string TargetGroupId, string EventType, string Summary)
        : SwarmEvent(SwarmId, DateTimeOffset.UtcNow);
}
