namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 67 — a named autonomous execution strategy that advances a mission
/// forward by one cycle. The default driver wraps <see cref="IMissionExecutor"/>
/// (LLM plan → engine run → acceptance gate); alternate drivers can swap in
/// different strategies (swarm, external orchestrator, rule-based) while
/// sharing the mission-store, journal, and state machine.
///
/// Drivers are looked up by <see cref="Name"/> through <see cref="DriverRegistry"/>
/// so a mission row can pin its execution strategy without the caller
/// needing to know the concrete type.
/// </summary>
public interface IAutonomousDriver
{
    /// <summary>Stable identifier used for registry lookup (e.g. "llm", "swarm").</summary>
    string Name { get; }

    /// <summary>Feature descriptor so callers can route work to a suitable driver.</summary>
    DriverCapabilities Capabilities { get; }

    /// <summary>
    /// Advances the mission forward by one driver cycle. Idempotent at the
    /// mission-state boundary: calling this on an already-terminal mission
    /// is a no-op that returns the current <see cref="Mission"/>.
    /// </summary>
    Task<Mission> AdvanceAsync(string missionId, CancellationToken ct = default);
}

/// <summary>Capability flags describing what an <see cref="IAutonomousDriver"/> supports.</summary>
public sealed record DriverCapabilities(
    bool SupportsReplanning,
    bool SupportsParallelSteps,
    bool SupportsHumanAcceptance,
    int MaxStepsPerCycle = 1);
