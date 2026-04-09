using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 51 — deterministic mission planner used as the default
/// <see cref="IMissionPlanner"/>. Produces a single-step plan whose
/// intent is the mission's goal and whose expected outcome is
/// "goal satisfied". This is intentionally minimal: the value of the
/// mission layer at this point is the orchestration + journal, not the
/// planner sophistication. An LLM-backed planner can slot in later
/// without touching the executor or store.
/// </summary>
public sealed class SimpleMissionPlanner : IMissionPlanner
{
    public Task<RuntimePlan> PlanAsync(Mission mission, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var step = new RuntimeStep(
            Index: 0,
            Intent: mission.Goal,
            ExpectedOutcome: "mission goal satisfied",
            ModelTier: RuntimeModelTier.Standard);

        var plan = new RuntimePlan(
            Id: $"plan-{Guid.NewGuid():N}",
            PlanVersion: 1,
            Goal: mission.Goal,
            Steps: new[] { step },
            CreatedAt: DateTimeOffset.UtcNow);

        return Task.FromResult(plan);
    }
}
