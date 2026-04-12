using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59e — produces one-line human-readable summaries of step
/// completion. Used by CLI and UI renderers to show progress.
/// </summary>
public static class StepSummaryEmitter
{
    /// <summary>
    /// Returns a one-line summary like "Step 2/5: Verified auth config exists [completed]".
    /// </summary>
    public static string Summarize(RuntimePlan plan, RuntimeStep step, string status)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(step);

        return $"Step {step.Index + 1}/{plan.Steps.Count}: {step.Intent} [{status}]";
    }

    /// <summary>
    /// Returns a one-line summary from a <see cref="StepOutcome"/>.
    /// </summary>
    public static string Summarize(RuntimePlan plan, RuntimeStep step, StepOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var status = outcome.Status switch
        {
            StepStatus.Succeeded => "completed",
            StepStatus.Skipped => "skipped",
            StepStatus.Failed => "failed",
            StepStatus.Contradicted => "contradicted",
#pragma warning disable CA1308 // UseToUpperInvariant — status strings are lowercase by convention
            _ => outcome.Status.ToString().ToLowerInvariant(),
#pragma warning restore CA1308
        };

        return Summarize(plan, step, status);
    }
}
