using Sovrant.Agents.Swarm;

namespace Sovrant.Agents.Orchestration;

/// <summary>
/// Phase 52 — unified request for the <see cref="IAgentOrchestrator"/>.
/// The three creation modes are determined by which fields are set:
/// <list type="bullet">
///   <item>Mode 1 (one team): <see cref="TeamId"/> is set</item>
///   <item>Mode 2 (multiple teams): <see cref="TeamIds"/> is set</item>
///   <item>Mode 3 (engine-decided): only <see cref="Goal"/> is set</item>
/// </list>
/// </summary>
public sealed class EnsembleRunRequest
{
    /// <summary>The task or goal to execute.</summary>
    public required string Goal { get; init; }

    /// <summary>Mode 1: run against a single pre-existing team.</summary>
    public string? TeamId { get; init; }

    /// <summary>Mode 2: run against multiple pre-existing teams.</summary>
    public IReadOnlyList<string>? TeamIds { get; init; }

    /// <summary>Pre-built plan (skips decomposition when provided).</summary>
    public IList<SwarmTaskNode>? Plan { get; init; }

    /// <summary>Workspace scope for the run.</summary>
    public required string WorkspaceId { get; init; }

    /// <summary>Optional project scope.</summary>
    public string? ProjectId { get; init; }

    /// <summary>User who triggered the run.</summary>
    public required string UserId { get; init; }

    /// <summary>Parent run ID for sub-spawns (swarm wave step, mission sub-step).</summary>
    public string? ParentRunId { get; init; }

    // ── Feature flags (opt-in heavy machinery) ──────────────────────────

    /// <summary>Whether to auto-decompose the goal into a DAG via <c>LlmSwarmDecomposer</c>.
    /// Defaults to <c>false</c> when <see cref="Plan"/> is supplied, <c>true</c> when only <see cref="Goal"/> is given.</summary>
    public bool? Decompose { get; init; }

    /// <summary>Whether to engage file-lock manager. Default: off for single-task, on for multi-task.</summary>
    public bool? LockFiles { get; init; }

    /// <summary>Whether to run the quality gate after execution. Default: off for delegations, on for swarms.</summary>
    public bool? QualityGate { get; init; }

    /// <summary>Maximum parallel task count. Default: 1 for delegations, <see cref="SwarmConfig.MaxConcurrent"/> for swarms.</summary>
    public int? MaxParallel { get; init; }

    /// <summary>Permission mode for agents: "ask", "accept-edits", "yolo".</summary>
    public string Permissions { get; init; } = "ask";

    /// <summary>Whether to publish ephemeral workers as a named team after completion.</summary>
    public bool PublishTeamOnComplete { get; init; }

    /// <summary>Event callback for SSE streaming.</summary>
    public Action<SwarmEvent>? OnEvent { get; init; }

    /// <summary>Phase 57 — whether to enable inter-group coordination via PM agents.</summary>
    public bool? EnableCoordination { get; init; }

    /// <summary>Phase 57 — PM agent template override (defaults to <c>pm-coordinator</c>).</summary>
    public string? PMTemplate { get; init; }
}
