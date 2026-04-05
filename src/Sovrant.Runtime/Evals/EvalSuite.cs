namespace Sovrant.Runtime.Evals;

/// <summary>A named collection of eval definitions with metadata.</summary>
public sealed class EvalSuite
{
    /// <summary>Name of the suite (e.g. "security-evals", "coding-capability").</summary>
    public required string Name { get; init; }

    /// <summary>Optional description of the suite's purpose.</summary>
    public string? Description { get; init; }

    /// <summary>The eval definitions in this suite.</summary>
    public required IReadOnlyList<EvalDefinition> Evals { get; init; }

    /// <summary>Optional tags applied to all evals in the suite.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
