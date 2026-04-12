using System.Text.RegularExpressions;
using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59d — default <see cref="IOrchestrationRouter"/> implementation.
/// Uses heuristics based on plan structure (step count, tool tiers, file
/// references, parallelism potential) to recommend an execution mode.
/// No LLM call — purely deterministic.
/// </summary>
public sealed partial class HeuristicOrchestrationRouter : IOrchestrationRouter
{
    [GeneratedRegex(@"[\\/][\w.]+\.\w{1,5}\b", RegexOptions.Compiled)]
    private static partial Regex FileRefPattern();

    /// <inheritdoc/>
    public OrchestrationRecommendation Recommend(RuntimePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var stepCount = plan.Steps.Count;
        var fileCount = EstimateFileCount(plan);
        var hasEscalation = HasEscalationTools(plan);
        var hasDangerous = HasDangerousTools(plan);
        var hasHighTierSteps = plan.Steps.Any(s => s.ModelTier == RuntimeModelTier.High);

        // ── Escalation guard: trivial tasks never get Swarm/Mission ────
        if (stepCount <= 2 && fileCount <= 1 && !hasEscalation)
        {
            return new OrchestrationRecommendation(
                OrchestrationMode.Direct,
                $"Simple task: {stepCount} step(s), {fileCount} file(s) — direct execution.",
                0.95f);
        }

        // ── Mission: open-ended goals with high-tier planning ──────────
        if (hasHighTierSteps && stepCount > 5 && hasEscalation)
        {
            return new OrchestrationRecommendation(
                OrchestrationMode.Mission,
                $"Complex goal with {stepCount} steps, high-tier planning, and escalation tools — mission mode.",
                0.7f);
        }

        // ── Swarm: many files or DAG-like dependencies ─────────────────
        if (fileCount >= 10 || stepCount >= 8)
        {
            return new OrchestrationRecommendation(
                OrchestrationMode.Swarm,
                $"Large scope: {stepCount} steps touching ~{fileCount} files — swarm decomposition.",
                0.75f);
        }

        // ── Team: moderate parallelism (3-10 files, independent steps) ─
        if (fileCount >= 3 && stepCount >= 3)
        {
            return new OrchestrationRecommendation(
                OrchestrationMode.Team,
                $"Moderate scope: {stepCount} steps across ~{fileCount} files — parallel team execution.",
                0.7f);
        }

        // ── SubAgent: sequential moderate tasks ────────────────────────
        if (stepCount > 2 || fileCount > 1 || hasDangerous)
        {
            return new OrchestrationRecommendation(
                OrchestrationMode.SubAgent,
                $"Moderate task: {stepCount} steps, {fileCount} file(s) — sub-agent delegation.",
                0.8f);
        }

        // ── Default: direct ────────────────────────────────────────────
        return new OrchestrationRecommendation(
            OrchestrationMode.Direct,
            $"Simple task: {stepCount} step(s), {fileCount} file(s) — direct execution.",
            0.9f);
    }

    /// <summary>
    /// Estimates the number of unique files referenced across all steps
    /// by scanning step intents and expected outcomes for file path patterns.
    /// </summary>
    private static int EstimateFileCount(RuntimePlan plan)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in plan.Steps)
        {
            foreach (Match match in FileRefPattern().Matches(step.Intent))
                files.Add(match.Value);
            foreach (Match match in FileRefPattern().Matches(step.ExpectedOutcome))
                files.Add(match.Value);
        }

        // If no explicit file references, estimate from step count.
        return files.Count > 0 ? files.Count : Math.Max(1, plan.Steps.Count / 2);
    }

    private static bool HasEscalationTools(RuntimePlan plan) =>
        plan.Steps.Any(s => s.AllowedTools is { Count: > 0 } tools &&
            tools.Any(t => GraduatedToolTiers.GetTier(t) == ToolTier.Escalation));

    private static bool HasDangerousTools(RuntimePlan plan) =>
        plan.Steps.Any(s => s.AllowedTools is { Count: > 0 } tools &&
            tools.Any(t => GraduatedToolTiers.GetTier(t) == ToolTier.Dangerous));
}
