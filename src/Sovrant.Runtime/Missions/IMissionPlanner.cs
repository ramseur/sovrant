using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 51 — produces a <see cref="RuntimePlan"/> for one mission step.
/// The mission planner sits above <see cref="IPlanner"/>: it decides what
/// the next engine run for a mission should do, and returns a concrete
/// plan the <see cref="IExecutor"/> can run.
///
/// The initial stub just wraps the mission's goal in a one-step
/// <see cref="RuntimePlan"/>; richer implementations can consult prior
/// events, split goals into phases, or delegate to an LLM.
/// </summary>
public interface IMissionPlanner
{
    Task<RuntimePlan> PlanAsync(Mission mission, CancellationToken ct = default);
}
