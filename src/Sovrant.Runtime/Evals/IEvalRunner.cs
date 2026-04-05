namespace Sovrant.Runtime.Evals;

/// <summary>Runs an eval suite and produces an aggregate report.</summary>
public interface IEvalRunner
{
    /// <summary>
    /// Runs all evals in the suite, grading each one using the configured grader,
    /// and returns an <see cref="EvalReport"/> with per-eval results and pass@k metrics.
    /// </summary>
    /// <param name="suite">The suite to run.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<EvalReport> RunAsync(EvalSuite suite, CancellationToken ct = default);
}
