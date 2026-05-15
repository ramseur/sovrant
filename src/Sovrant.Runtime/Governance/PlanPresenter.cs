using System.Globalization;
using System.Text;
using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59b — default <see cref="IPlanPresenter"/> implementation.
/// Produces a numbered step list with tool tier warnings for destructive steps.
/// </summary>
public sealed class PlanPresenter : IPlanPresenter
{
    /// <inheritdoc/>
    public string FormatPlan(RuntimePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var sb = new StringBuilder();
        sb.Append("Plan: ").AppendLine(plan.Goal);
        sb.AppendLine();

        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            var stepNum = i + 1;
            var tierMarker = GetTierMarker(step);

            sb.Append(CultureInfo.InvariantCulture, $"  ({stepNum}) {step.Intent}");
            if (tierMarker is not null)
                sb.Append(CultureInfo.InvariantCulture, $" {tierMarker}");
            sb.AppendLine();

            if (step.AllowedTools is { Count: > 0 })
                sb.AppendLine(CultureInfo.InvariantCulture, $"      Tools: {string.Join(", ", step.AllowedTools)}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns a warning marker for steps that contain destructive or
    /// escalation-tier tools. Returns null for safe steps.
    /// </summary>
    private static string? GetTierMarker(RuntimeStep step)
    {
        if (step.AllowedTools is not { Count: > 0 })
            return null;

        var maxTier = step.AllowedTools
            .Select(GraduatedToolTiers.GetTier)
            .DefaultIfEmpty(ToolTier.Safe)
            .Max();

        return maxTier switch
        {
            ToolTier.Dangerous => "[DANGEROUS]",
            ToolTier.Escalation => "[ESCALATION]",
            _ => null,
        };
    }
}
