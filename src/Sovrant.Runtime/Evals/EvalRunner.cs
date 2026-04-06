using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Evals.Graders;

namespace Sovrant.Runtime.Evals;

/// <summary>
/// Runs an eval suite: for each eval, executes the prompt (simulated as the eval output),
/// grades the result using the configured grader, and computes pass@k metrics.
/// </summary>
public sealed partial class EvalRunner : IEvalRunner
{
    private readonly ISmartRouter _router;
    private readonly IEvalResultStore _resultStore;
    private readonly ILogger<EvalRunner> _logger;

    public EvalRunner(ISmartRouter router, IEvalResultStore resultStore, ILogger<EvalRunner> logger)
    {
        _router = router;
        _resultStore = resultStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<EvalReport> RunAsync(EvalSuite suite, CancellationToken ct = default) =>
        RunAsync(suite, new PassThroughOutputProvider(), ct);

    /// <summary>
    /// Runs all evals in the suite using the given output provider to produce agent output.
    /// </summary>
    public async Task<EvalReport> RunAsync(EvalSuite suite, IEvalOutputProvider outputProvider, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(suite);
        ArgumentNullException.ThrowIfNull(outputProvider);

        var startedAt = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        var results = new List<EvalResult>();

        LogSuiteStarted(suite.Name, suite.Evals.Count);

        foreach (var eval in suite.Evals)
        {
            ct.ThrowIfCancellationRequested();
            var result = await RunSingleEvalAsync(eval, outputProvider, ct).ConfigureAwait(false);
            results.Add(result);

            var status = result.Skipped ? "SKIP" : result.Passed ? "PASS" : "FAIL";
            LogEvalResult(status, eval.Name);
        }

        sw.Stop();

        var report = new EvalReport
        {
            SuiteName = suite.Name,
            Results = results,
            StartedAt = startedAt,
            Duration = sw.Elapsed,
        };

        // Persist results for trend tracking
        try
        {
            await _resultStore.SaveAsync(report, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveResultsFailed(ex, suite.Name);
        }

        return report;
    }

    private async Task<EvalResult> RunSingleEvalAsync(
        EvalDefinition eval, IEvalOutputProvider outputProvider, CancellationToken ct)
    {
        var grader = CreateGrader(eval.Grader);

        // Human grader evals are skipped (pending human review)
        if (eval.Grader.Type == GraderType.Human)
        {
            return new EvalResult
            {
                EvalName = eval.Name,
                Category = eval.Category,
                GraderType = eval.Grader.Type,
                Attempts = [],
                Passed = false,
                Skipped = true,
            };
        }

        var attempts = new List<GradeResult>();
        var passCount = 0;

        for (int i = 0; i < eval.Attempts; i++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var output = await outputProvider.GetOutputAsync(eval, i, ct).ConfigureAwait(false);
                var grade = await grader.GradeAsync(eval, output, ct).ConfigureAwait(false);
                attempts.Add(grade);
                if (grade.Passed) passCount++;

                // Early exit if we've already met the threshold
                if (passCount >= eval.PassThreshold)
                    break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogEvalAttemptFailed(ex, eval.Name, i + 1);
                attempts.Add(new GradeResult(false, $"Error: {ex.Message}", Duration: TimeSpan.Zero));
            }
        }

        return new EvalResult
        {
            EvalName = eval.Name,
            Category = eval.Category,
            GraderType = eval.Grader.Type,
            Attempts = attempts,
            Passed = passCount >= eval.PassThreshold,
        };
    }

    private IGrader CreateGrader(GraderConfig config) => config.Type switch
    {
        GraderType.Code => new CodeGrader(),
        GraderType.Model => new ModelGrader(_router, _logger),
        GraderType.Human => new HumanGrader(),
        _ => new CodeGrader(),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting eval suite: {SuiteName} ({EvalCount} evals)")]
    private partial void LogSuiteStarted(string suiteName, int evalCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "  {Status} {EvalName}")]
    private partial void LogEvalResult(string status, string evalName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to save eval results for suite {SuiteName}")]
    private partial void LogSaveResultsFailed(Exception ex, string suiteName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Eval {EvalName} attempt {Attempt} failed with error")]
    private partial void LogEvalAttemptFailed(Exception ex, string evalName, int attempt);
}

/// <summary>
/// Provides the output text for an eval attempt.
/// In a full integration, this would run the agent against the eval prompt.
/// </summary>
public interface IEvalOutputProvider
{
    /// <summary>
    /// Produces the agent output for the given eval and attempt number.
    /// </summary>
    Task<string> GetOutputAsync(EvalDefinition eval, int attemptIndex, CancellationToken ct = default);
}

/// <summary>
/// Simple output provider that returns the eval prompt as the output.
/// Used for code-grader evals where the check command operates on files or external state
/// rather than the agent's text output.
/// </summary>
internal sealed class PassThroughOutputProvider : IEvalOutputProvider
{
    public Task<string> GetOutputAsync(EvalDefinition eval, int attemptIndex, CancellationToken ct = default) =>
        Task.FromResult(eval.Prompt);
}
