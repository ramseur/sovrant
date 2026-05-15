using System.Globalization;
using System.Text;

namespace Sovrant.Runtime.Evals;

/// <summary>Result of a single grading attempt.</summary>
public sealed record GradeResult(
    bool Passed,
    string Output,
    double? Score = null,
    TimeSpan Duration = default);

/// <summary>Result of a single eval across all its attempts.</summary>
public sealed class EvalResult
{
    /// <summary>Name of the eval that was run.</summary>
    public required string EvalName { get; init; }

    /// <summary>Category of the eval.</summary>
    public EvalCategory Category { get; init; }

    /// <summary>Individual attempt results.</summary>
    public required IReadOnlyList<GradeResult> Attempts { get; init; }

    /// <summary>Whether the eval passed (met pass threshold).</summary>
    public required bool Passed { get; init; }

    /// <summary>pass@1 — true if the first attempt passed.</summary>
    public bool PassAt1 => Attempts.Count > 0 && Attempts[0].Passed;

    /// <summary>pass@k — true if at least <see cref="EvalDefinition.PassThreshold"/> attempts passed.</summary>
    public bool PassAtK => Passed;

    /// <summary>Total number of passes across all attempts.</summary>
    public int PassCount => Attempts.Count(a => a.Passed);

    /// <summary>Average score across scored attempts (null if no scores).</summary>
    public double? AverageScore
    {
        get
        {
            var scored = Attempts.Where(a => a.Score.HasValue).ToList();
            return scored.Count > 0 ? scored.Average(a => a.Score!.Value) : null;
        }
    }

    /// <summary>Total duration across all attempts.</summary>
    public TimeSpan TotalDuration => Attempts.Aggregate(TimeSpan.Zero, (sum, a) => sum + a.Duration);

    /// <summary>Grader type for reporting purposes.</summary>
    public GraderType GraderType { get; init; }

    /// <summary>If the eval was skipped (e.g. human grader), this is true.</summary>
    public bool Skipped { get; init; }
}

/// <summary>Aggregate report for an entire eval suite run.</summary>
public sealed class EvalReport
{
    /// <summary>Name of the suite that was evaluated.</summary>
    public required string SuiteName { get; init; }

    /// <summary>Per-eval results.</summary>
    public required IReadOnlyList<EvalResult> Results { get; init; }

    /// <summary>When the eval run started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Total duration of the eval run.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Overall pass rate (0.0–1.0).</summary>
    public double PassRate
    {
        get
        {
            var graded = Results.Where(r => !r.Skipped).ToList();
            return graded.Count > 0 ? (double)graded.Count(r => r.Passed) / graded.Count : 0.0;
        }
    }

    /// <summary>pass@1 rate — fraction of evals that passed on first attempt.</summary>
    public double PassAt1Rate
    {
        get
        {
            var graded = Results.Where(r => !r.Skipped).ToList();
            return graded.Count > 0 ? (double)graded.Count(r => r.PassAt1) / graded.Count : 0.0;
        }
    }

    /// <summary>Number of evals that passed.</summary>
    public int TotalPassed => Results.Count(r => r.Passed && !r.Skipped);

    /// <summary>Number of evals that failed.</summary>
    public int TotalFailed => Results.Count(r => !r.Passed && !r.Skipped);

    /// <summary>Number of evals skipped (e.g. human grader).</summary>
    public int TotalSkipped => Results.Count(r => r.Skipped);

    /// <summary>Formats the report as a human-readable summary.</summary>
    public string ToReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Eval Report: {SuiteName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Run at {StartedAt:yyyy-MM-dd HH:mm:ss} — {Duration.TotalSeconds:F1}s total");
        sb.AppendLine();

        var maxName = Results.Count > 0 ? Results.Max(r => r.EvalName.Length) : 0;

        foreach (var result in Results)
        {
            var icon = result.Skipped ? "⊘" : result.Passed ? "✓" : "✗";
            var status = result.Skipped ? "SKIP" : result.Passed ? "PASS" : "FAIL";
            var details = result.Attempts.Count > 1
                ? $" ({result.PassCount}/{result.Attempts.Count} passed)"
                : string.Empty;
            var score = result.AverageScore.HasValue
                ? string.Create(CultureInfo.InvariantCulture, $" score={result.AverageScore:F1}")
                : string.Empty;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {icon} {result.EvalName.PadRight(maxName)}  {status}{details}{score}  [{result.TotalDuration.TotalSeconds:F1}s]");
        }

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"pass@1: {PassAt1Rate:P0}  |  pass@k: {PassRate:P0}  |  {TotalPassed} passed, {TotalFailed} failed, {TotalSkipped} skipped");
        return sb.ToString();
    }
}
