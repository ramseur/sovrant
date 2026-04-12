using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59d — execution modes the orchestration router can recommend.
/// </summary>
public enum OrchestrationMode
{
    /// <summary>Single model execution — simple tasks (1-2 steps, 1 file).</summary>
    Direct,

    /// <summary>Delegated to a single sub-agent — moderate tasks (2-5 files, sequential).</summary>
    SubAgent,

    /// <summary>Parallel independent agents — team tasks (3-10 files, independent steps).</summary>
    Team,

    /// <summary>DAG-based decomposition — complex tasks (10+ files or dependency graph).</summary>
    Swarm,

    /// <summary>Open-ended goal with replanning — long-running exploratory tasks.</summary>
    Mission,
}

/// <summary>
/// Phase 59d — the router's recommendation with reasoning and confidence.
/// </summary>
public sealed record OrchestrationRecommendation(
    OrchestrationMode Mode,
    string Reasoning,
    float Confidence);

/// <summary>
/// Phase 59d — analyses task complexity from a <see cref="RuntimePlan"/>
/// and recommends which execution mode should be used. The recommendation
/// is advisory — the LLM can override it with explanation.
/// </summary>
public interface IOrchestrationRouter
{
    /// <summary>Recommends an execution mode based on plan structure.</summary>
    OrchestrationRecommendation Recommend(RuntimePlan plan);
}
