namespace Sovrant.Runtime.Evals;

/// <summary>The type of grader used to evaluate an eval's output.</summary>
public enum GraderType
{
    /// <summary>Deterministic: runs a command, checks exit code / output pattern.</summary>
    Code,

    /// <summary>LLM judges the output against a rubric.</summary>
    Model,

    /// <summary>Flags for manual review, stores rating.</summary>
    Human,
}

/// <summary>The category of eval — capability, regression, or quality.</summary>
public enum EvalCategory
{
    /// <summary>Can the agent do X?</summary>
    Capability,

    /// <summary>Does Y still work after changes?</summary>
    Regression,

    /// <summary>How well does the agent do X?</summary>
    Quality,
}

/// <summary>Configuration for how an eval's output is graded.</summary>
public sealed class GraderConfig
{
    /// <summary>The type of grader to use.</summary>
    public GraderType Type { get; init; }

    /// <summary>For <see cref="GraderType.Code"/>: the command to run. <c>{output}</c> is replaced with the eval output.</summary>
    public string? Command { get; init; }

    /// <summary>For <see cref="GraderType.Code"/>: regex pattern the output must match for a pass.</summary>
    public string? Pattern { get; init; }

    /// <summary>For <see cref="GraderType.Model"/>: the rubric prompt sent to the LLM for judging.</summary>
    public string? Rubric { get; init; }

    /// <summary>For <see cref="GraderType.Model"/>: optional model override (e.g. "gpt-4o").</summary>
    public string? Model { get; init; }
}

/// <summary>A single eval: name, prompt, expected behavior, grader config.</summary>
public sealed class EvalDefinition
{
    /// <summary>Unique name identifying this eval (e.g. "security-review-detects-sqli").</summary>
    public required string Name { get; init; }

    /// <summary>The prompt to send to the agent being evaluated.</summary>
    public required string Prompt { get; init; }

    /// <summary>Category of the eval.</summary>
    public EvalCategory Category { get; init; } = EvalCategory.Capability;

    /// <summary>How to grade the output.</summary>
    public required GraderConfig Grader { get; init; }

    /// <summary>Number of attempts for pass@k computation. 1 = pass@1 only.</summary>
    public int Attempts { get; init; } = 1;

    /// <summary>Minimum passes out of <see cref="Attempts"/> for the eval to be considered passing.</summary>
    public int PassThreshold { get; init; } = 1;

    /// <summary>Optional tags for filtering (e.g. "security", "coding", "research").</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Optional description of what this eval tests.</summary>
    public string? Description { get; init; }
}
