using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;

namespace Sovrant.Runtime.Evals.Graders;

/// <summary>
/// LLM-based grader: sends the eval output to an LLM with a rubric prompt
/// and parses the response for a pass/fail verdict and optional numeric score.
/// </summary>
internal sealed partial class ModelGrader : IGrader
{
    private readonly ISmartRouter _router;
    private readonly ILogger _logger;

    public ModelGrader(ISmartRouter router, ILogger logger)
    {
        _router = router;
        _logger = logger;
    }

    public async Task<GradeResult> GradeAsync(EvalDefinition eval, string output, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var grader = eval.Grader;
        var rubric = grader.Rubric ?? "Evaluate the following output and respond with PASS or FAIL.";

        var systemPrompt = """
            You are an eval grader. Given a rubric and agent output, you MUST respond with a structured verdict.

            Your response MUST contain exactly one of these on its own line:
            VERDICT: PASS
            VERDICT: FAIL

            Optionally include a numeric score (1-10) on its own line:
            SCORE: <number>

            Then provide a brief explanation.
            """;

        var userMessage = $"""
            ## Rubric
            {rubric}

            ## Eval: {eval.Name}
            {eval.Description ?? eval.Prompt}

            ## Agent Output
            {output}

            Evaluate the output against the rubric. Respond with VERDICT: PASS or VERDICT: FAIL, optionally SCORE: <1-10>, then a brief explanation.
            """;

        var model = grader.Model ?? "default";
        var request = new MessagesRequest(model, 1024, [InputMessage.UserText(userMessage)])
        {
            System = systemPrompt,
        };

        try
        {
            var provider = await _router.RouteAsync(request, ct).ConfigureAwait(false);
            var result = await provider.SendAsync(request, ct).ConfigureAwait(false);
            sw.Stop();

            if (!result.IsSuccess)
            {
                var errorMsg = result.Error?.Message ?? "Unknown LLM error";
                LogLlmCallFailed(eval.Name, errorMsg);
                return new GradeResult(false, $"LLM grading failed: {errorMsg}", Duration: sw.Elapsed);
            }

            var responseText = ExtractText(result.Value!);
            var passed = ParseVerdict(responseText);
            var score = ParseScore(responseText);

            return new GradeResult(passed, responseText, score, sw.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            LogModelGraderFailed(ex, eval.Name);
            return new GradeResult(false, $"LLM grading error: {ex.Message}", Duration: sw.Elapsed);
        }
    }

    private static string ExtractText(MessageResponse response) =>
        string.Join("\n", response.Content
            .OfType<OutputContentBlock.TextBlock>()
            .Select(b => b.Text));

    private static bool ParseVerdict(string text) =>
        VerdictPattern().IsMatch(text);

    private static double? ParseScore(string text)
    {
        var match = ScorePattern().Match(text);
        if (match.Success && double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var score))
            return Math.Clamp(score, 1.0, 10.0);
        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "ModelGrader LLM call failed for eval {EvalName}: {ErrorMessage}")]
    private partial void LogLlmCallFailed(string evalName, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ModelGrader failed for eval {EvalName}")]
    private partial void LogModelGraderFailed(Exception ex, string evalName);

    [GeneratedRegex(@"VERDICT:\s*PASS", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VerdictPattern();

    [GeneratedRegex(@"SCORE:\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ScorePattern();
}
