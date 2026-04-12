using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59b — configurable approval modes for plan execution.
/// </summary>
public enum PlanApprovalMode
{
    /// <summary>Never ask — auto-execute all plans.</summary>
    AlwaysApprove,

    /// <summary>Ask only when the plan contains Dangerous or Escalation tier tools.</summary>
    ApproveDestructive,

    /// <summary>Ask for every plan before execution.</summary>
    AlwaysAsk,
}

/// <summary>
/// Phase 59b — determines whether a <see cref="RuntimePlan"/> requires
/// user approval before execution can begin. The approval mode is
/// configurable per workspace/project.
/// </summary>
public sealed class PlanApprovalGate
{
    /// <summary>
    /// Returns <see langword="true"/> if the plan requires user approval
    /// under the given mode.
    /// </summary>
    public static bool RequiresApproval(RuntimePlan plan, PlanApprovalMode mode)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return mode switch
        {
            PlanApprovalMode.AlwaysApprove => false,
            PlanApprovalMode.AlwaysAsk => true,
            PlanApprovalMode.ApproveDestructive => HasDestructiveOrEscalationTools(plan),
            _ => true,
        };
    }

    /// <summary>
    /// Checks whether any step in the plan uses tools at the Dangerous
    /// or Escalation tier.
    /// </summary>
    private static bool HasDestructiveOrEscalationTools(RuntimePlan plan)
    {
        foreach (var step in plan.Steps)
        {
            if (step.AllowedTools is not { Count: > 0 })
                continue;

            foreach (var tool in step.AllowedTools)
            {
                var tier = GraduatedToolTiers.GetTier(tool);
                if (tier >= ToolTier.Dangerous)
                    return true;
            }
        }

        // If no steps have explicit allowed tools, check step count and
        // model tier as heuristic — multi-step plans with High tier are
        // likely complex enough to warrant review.
        return plan.Steps.Count > 3;
    }
}
