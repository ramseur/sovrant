namespace Sovrant.Runtime.Evals.Graders;

/// <summary>Grades the output of an eval attempt.</summary>
internal interface IGrader
{
    /// <summary>
    /// Evaluates the output produced by an agent for a given eval definition.
    /// </summary>
    /// <param name="eval">The eval definition being graded.</param>
    /// <param name="output">The agent's output text to evaluate.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<GradeResult> GradeAsync(EvalDefinition eval, string output, CancellationToken ct = default);
}
