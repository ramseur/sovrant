namespace Sovrant.Runtime.Engine;

/// <summary>
/// Phase 51 — a structured runtime plan produced by an <see cref="IPlanner"/>
/// and consumed by an <see cref="IExecutor"/>. This is the planner/executor
/// split: instead of letting the model thrash inside one loop choosing a tool
/// on every call, the planner commits to a short sequence of intended steps
/// and the executor runs them deliberately, reporting failures back as
/// re-plan requests rather than as silent wrong turns.
///
/// A plan is *intentionally small* — typically 3–10 steps — because re-plans
/// are cheap (patches against the existing plan) and the whole point is to
/// notice contradictions early and correct course, not to lock a 50-step
/// pipeline the moment a goal arrives.
/// </summary>
/// <param name="Id">Stable identifier for this plan version. A re-plan produces a new id.</param>
/// <param name="PlanVersion">1 for the initial plan; incremented on every re-plan patch.</param>
/// <param name="Goal">The natural-language goal the planner was given when producing this plan.</param>
/// <param name="Steps">Ordered list of steps. The executor advances through them in order; re-plans may insert, remove, or rewrite any suffix.</param>
/// <param name="CreatedAt">When this plan version was produced.</param>
public sealed record RuntimePlan(
    string Id,
    int PlanVersion,
    string Goal,
    IReadOnlyList<RuntimeStep> Steps,
    DateTimeOffset CreatedAt);

/// <summary>
/// A single step in a <see cref="RuntimePlan"/>. Carries the planner's
/// *intent* (why this step exists), the expected outcome (so the executor
/// can notice contradictions), the model tier it should run on (so per-step
/// routing actually happens), and an optional tool allow-list that narrows
/// what the executor may call inside this step.
/// </summary>
/// <param name="Index">Position within the parent plan's <c>Steps</c> list.</param>
/// <param name="Intent">Planner's stated reason for this step — e.g. "confirm the function exists before editing it". Persisted alongside every tool call in <c>runtime_traces</c>.</param>
/// <param name="ExpectedOutcome">Planner's prediction of what the step should produce. A mismatch between this and the actual outcome is one of the triggers for a re-plan.</param>
/// <param name="ModelTier">Which Phase 22 model tier the executor should route this step to (<c>high</c> / <c>standard</c> / <c>fast</c>).</param>
/// <param name="AllowedTools">Optional narrowing of the tool set for this step. <c>null</c> means "inherit the session allow-list".</param>
/// <param name="MacroName">If set, this step is a macro expansion — the executor runs the named <see cref="MacroDefinition"/> inline instead of a single tool call.</param>
public sealed record RuntimeStep(
    int Index,
    string Intent,
    string ExpectedOutcome,
    RuntimeModelTier ModelTier,
    IReadOnlyList<string>? AllowedTools = null,
    string? MacroName = null);

/// <summary>
/// Phase 22 model tiers, restated for the planner/executor contract so
/// the engine layer does not take a direct dependency on the router's
/// internal tier enum. The executor maps this to the concrete provider
/// via <c>SmartRouter</c> per step.
/// </summary>
public enum RuntimeModelTier
{
    /// <summary>High-capability model. Used for planning, ambiguous reasoning, acceptance checks.</summary>
    High,

    /// <summary>Standard model. Used for most code edits, tool calls with clear semantics, and normal execution.</summary>
    Standard,

    /// <summary>Fast / cheap model. Used for mechanical operations: grep, read, parse, compaction summarisation.</summary>
    Fast,
}
