using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59c — injects the current step's intent and allowed tools into
/// the system prompt so the LLM is constrained to the planner's stated
/// purpose for each step.
/// </summary>
public sealed class IntentInjector
{
    private const string Separator = "\n\n--- Step Context (Phase 59) ---\n";

    /// <summary>
    /// Returns a new system prompt with the step intent and allowed tools appended.
    /// </summary>
    public static string InjectIntent(string systemPrompt, RuntimeStep step)
    {
        ArgumentNullException.ThrowIfNull(systemPrompt);
        ArgumentNullException.ThrowIfNull(step);

        var sb = new System.Text.StringBuilder(systemPrompt.Length + 256);
        sb.Append(systemPrompt);
        sb.Append(Separator);
        sb.Append("Current step intent: ").AppendLine(step.Intent);
        sb.Append("Expected outcome: ").AppendLine(step.ExpectedOutcome);

        if (step.AllowedTools is { Count: > 0 })
        {
            sb.Append("Allowed tools for this step: ")
              .AppendLine(string.Join(", ", step.AllowedTools));
            sb.AppendLine("Do NOT use tools outside this list.");
        }

        return sb.ToString();
    }
}
