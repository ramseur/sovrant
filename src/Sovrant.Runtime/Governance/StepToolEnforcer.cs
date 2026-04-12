using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59c — validates that a tool call is permitted within the context
/// of the current <see cref="RuntimeStep"/>. Checks the step's optional
/// <c>AllowedTools</c> list and the graduated tool tier.
/// </summary>
public sealed class StepToolEnforcer
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="toolName"/> is
    /// allowed in the given step.
    /// </summary>
    /// <param name="toolName">The tool being invoked.</param>
    /// <param name="currentStep">The currently executing step (may be <c>null</c> outside engine mode).</param>
    public static bool IsAllowed(string toolName, RuntimeStep? currentStep)
    {
        ArgumentNullException.ThrowIfNull(toolName);

        // No step context or no allow-list → inherit session-level permissions.
        if (currentStep?.AllowedTools is not { Count: > 0 } allowedTools)
            return true;

        // Check the step-level allow-list (case-insensitive).
        return allowedTools.Any(t => string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns a structured denial reason when a tool is not in the step's allow-list.
    /// </summary>
    public static string DenialReason(string toolName, RuntimeStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return $"Tool '{toolName}' is not in the allow-list for step {step.Index} (\"{step.Intent}\"). " +
            $"Allowed: [{string.Join(", ", step.AllowedTools ?? [])}].";
    }
}
