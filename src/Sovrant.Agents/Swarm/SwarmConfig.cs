using System.Text.Json.Serialization;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Configuration for the swarm orchestrator. OFF by default.
/// Loaded from <c>.sovrant/swarm.json</c>; designed for SQLite migration in Phase 31.
/// </summary>
public sealed class SwarmConfig
{
    /// <summary>Whether the swarm orchestrator is enabled. Defaults to <c>false</c>.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    /// <summary>Maximum agents running concurrently within a wave.</summary>
    [JsonPropertyName("max_concurrent")]
    public int MaxConcurrent { get; init; } = 4;

    /// <summary>Token budget ceiling (input + output tokens across all tasks).</summary>
    [JsonPropertyName("max_token_budget")]
    public int MaxTokenBudget { get; init; } = 500_000;

    /// <summary>Per-task retry count on failure.</summary>
    [JsonPropertyName("max_retries")]
    public int MaxRetries { get; init; } = 1;

    /// <summary>Whether to run a quality gate review after execution.</summary>
    [JsonPropertyName("quality_gate")]
    public bool QualityGateEnabled { get; init; } = true;

    /// <summary>Recommended model level for the decomposer agent.</summary>
    [JsonPropertyName("decomposer_level")]
    public string DecomposerLevel { get; init; } = "High";

    /// <summary>Recommended model level for worker agents.</summary>
    [JsonPropertyName("worker_level")]
    public string WorkerLevel { get; init; } = "Standard";

    /// <summary>Per-task timeout in seconds.</summary>
    [JsonPropertyName("task_timeout_seconds")]
    public int TaskTimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Override agent templates per task type (e.g. <c>{ "code_change": "coder", "review": "security-reviewer" }</c>).
    /// </summary>
    [JsonPropertyName("templates")]
    public Dictionary<string, string> TemplateOverrides { get; init; } = [];
}
