using System.Diagnostics;

namespace Sovrant.Runtime.Evals.Graders;

/// <summary>
/// Human grader: marks the eval as skipped/pending human review.
/// The output is preserved so a human can review and rate it later.
/// </summary>
internal sealed class HumanGrader : IGrader
{
    public Task<GradeResult> GradeAsync(EvalDefinition eval, string output, CancellationToken ct = default) =>
        Task.FromResult(new GradeResult(
            Passed: false,
            Output: $"[Pending human review]\n\n{output}",
            Score: null,
            Duration: TimeSpan.Zero));
}
