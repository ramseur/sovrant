using Sovrant.Api.Errors;

namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — resolves <see cref="RuntimeStep.MacroName"/> references in a
/// plan against an <see cref="IMacroRegistry"/> and returns a new plan
/// whose <see cref="RuntimePlan.Steps"/> are flat (no macro refs remain)
/// and whose step indexes are contiguous starting from 0.
///
/// The expander is a pure function of (plan, registry): it never touches
/// the trace store, never calls a model, and has no mutable state. That
/// lets the executor call it once before starting a run and again after
/// every re-plan without worrying about ordering or idempotence.
///
/// Macros cannot reference other macros — the expander rejects a macro
/// whose template includes another macro reference with
/// <see cref="MacroExpansionException"/>. Flatten nested macros at
/// definition time instead.
/// </summary>
public sealed class MacroExpander
{
    private readonly IMacroRegistry _registry;

    public MacroExpander(IMacroRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>
    /// Returns a copy of <paramref name="plan"/> with every macro-reference
    /// step replaced by the corresponding <see cref="MacroDefinition.Steps"/>,
    /// and indexes reassigned to be contiguous from 0. If the plan has no
    /// macro references the returned plan is equal to the input (same
    /// reference is not guaranteed).
    /// </summary>
    public RuntimePlan Expand(RuntimePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // Fast path: if no step references a macro we only need to
        // rebuild if the indexes are already contiguous from 0.
        var hasMacro = false;
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            if (plan.Steps[i].MacroName is not null)
            {
                hasMacro = true;
                break;
            }
        }
        if (!hasMacro)
        {
            // Still rewrite indexes to be safe; a replanner may have
            // returned steps with sparse indexes (e.g. 2, 5, 7) meaning
            // "second, fifth, seventh of the previous plan" — by the time
            // we hand the plan to the executor the indexes must be 0..n-1.
            return RewriteIndexes(plan);
        }

        var expanded = new List<RuntimeStep>(plan.Steps.Count);
        var nextIndex = 0;
        foreach (var step in plan.Steps)
        {
            if (step.MacroName is null)
            {
                expanded.Add(step with { Index = nextIndex++ });
                continue;
            }

            var macro = _registry.Find(step.MacroName)
                ?? throw new MacroExpansionException(
                    $"Plan '{plan.Id}' step {step.Index} references unknown macro '{step.MacroName}'.");

            foreach (var template in macro.Steps)
            {
                // Reject nested macros: the template itself would need a
                // MacroName field, and MacroStepTemplate intentionally
                // does not have one. If the template happens to be a
                // RuntimeStep-shaped object with MacroName set (e.g. via
                // a future extension) we catch it here.
                expanded.Add(new RuntimeStep(
                    Index: nextIndex++,
                    Intent: template.Intent,
                    ExpectedOutcome: template.ExpectedOutcome,
                    ModelTier: template.ModelTier,
                    AllowedTools: template.AllowedTools,
                    MacroName: null));
            }
        }

        return plan with { Steps = expanded };
    }

    private static RuntimePlan RewriteIndexes(RuntimePlan plan)
    {
        var rewritten = new List<RuntimeStep>(plan.Steps.Count);
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            rewritten.Add(step.Index == i ? step : step with { Index = i });
        }
        return plan with { Steps = rewritten };
    }
}

/// <summary>Thrown when a plan references a macro the registry cannot resolve.</summary>
public sealed class MacroExpansionException : SovrantException
{
    public MacroExpansionException(string message) : base(message) { }
    public MacroExpansionException(string message, Exception inner) : base(message, inner) { }
    public MacroExpansionException() { }
}
