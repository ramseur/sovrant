namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — a named, reusable sequence of step templates the planner
/// can reference by name instead of restating the same sub-procedure on
/// every run. A macro reference in a plan is just a <see cref="RuntimeStep"/>
/// with its <see cref="RuntimeStep.MacroName"/> set; the
/// <see cref="MacroExpander"/> resolves it against an
/// <see cref="IMacroRegistry"/> before the plan reaches the executor.
///
/// Macros are *expanded*, not *invoked* — once expanded, the resulting
/// plan is a flat list of concrete steps that the executor runs exactly
/// like any other step. This keeps the executor's contract tiny (no
/// nested execution scopes, no recursive retry budgets) at the cost of
/// macro definitions needing to be fully inlinable.
///
/// Example use cases:
/// - "bisect a failing test": narrow, read, run, read, narrow, run...
/// - "write a module": sketch, scaffold, test, document, tidy
/// </summary>
/// <param name="Name">Stable identifier the planner uses to reference this macro.</param>
/// <param name="Description">Short prose description the planner shows itself when deciding whether to reference the macro.</param>
/// <param name="Steps">Ordered step templates the expander inlines in place of the macro reference. Each template's index is reassigned by the expander, so templates should leave <see cref="MacroStepTemplate"/> indexes implicit.</param>
public sealed record MacroDefinition(
    string Name,
    string Description,
    IReadOnlyList<MacroStepTemplate> Steps);

/// <summary>
/// One step inside a <see cref="MacroDefinition"/>. Mirrors
/// <see cref="RuntimeStep"/> minus the <c>Index</c> (which the expander
/// assigns) and minus the <c>MacroName</c> (macros cannot reference
/// other macros — flatten them at definition time if you need that).
/// </summary>
public sealed record MacroStepTemplate(
    string Intent,
    string ExpectedOutcome,
    RuntimeModelTier ModelTier,
    IReadOnlyList<string>? AllowedTools = null);

/// <summary>
/// Lookup table for <see cref="MacroDefinition"/>s. Kept tiny so tests
/// can stand up a fake registry with two lines, and so the production
/// registry can load from disk / DB without the expander caring.
/// </summary>
public interface IMacroRegistry
{
    /// <summary>Returns the definition for <paramref name="name"/> or <c>null</c> if the registry does not know it.</summary>
    MacroDefinition? Find(string name);
}

/// <summary>
/// Trivial dictionary-backed registry. Construct once at startup with
/// the macros the current engine run should see. Thread-safe for reads;
/// the set of known macros is fixed at construction time.
/// </summary>
public sealed class InMemoryMacroRegistry : IMacroRegistry
{
    private readonly Dictionary<string, MacroDefinition> _macros;

    public InMemoryMacroRegistry(IEnumerable<MacroDefinition> macros)
    {
        ArgumentNullException.ThrowIfNull(macros);
        _macros = macros.ToDictionary(m => m.Name, StringComparer.Ordinal);
    }

    public MacroDefinition? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _macros.TryGetValue(name, out var macro) ? macro : null;
    }
}
