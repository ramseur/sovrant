using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59e — tracks step completion against the total plan and produces
/// <see cref="RuntimeEvent.StepProgress"/> events that UIs can display
/// as progress indicators.
/// </summary>
public sealed class PlanProgressTracker
{
    /// <summary>
    /// Creates a <see cref="RuntimeEvent.StepProgress"/> event for the
    /// given plan, step, and status.
    /// </summary>
    public static RuntimeEvent.StepProgress CreateEvent(
        RuntimePlan plan, RuntimeStep step, string status)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(step);

        return new RuntimeEvent.StepProgress(
            Current: step.Index + 1,
            Total: plan.Steps.Count,
            Intent: step.Intent,
            Status: status);
    }
}
