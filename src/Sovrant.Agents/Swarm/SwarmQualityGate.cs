using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Post-execution quality gate that reviews combined swarm output
/// and returns a <see cref="QualityVerdict"/>.
/// </summary>
public sealed partial class SwarmQualityGate : ISwarmQualityGate
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly SovrantAgentFactory _factory;
    private readonly ILogger<SwarmQualityGate> _logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running quality gate review for swarm '{SwarmId}'")]
    private static partial void LogStarting(ILogger logger, string swarmId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Quality gate verdict for '{SwarmId}': {Verdict} (score={Score})")]
    private static partial void LogVerdict(ILogger logger, string swarmId, string verdict, int score);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse quality gate response for '{SwarmId}': {Error}")]
    private static partial void LogParseFailed(ILogger logger, string swarmId, string error);

    public SwarmQualityGate(SovrantAgentFactory factory, ILogger<SwarmQualityGate> logger)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(logger);
        _factory = factory;
        _logger = logger;
    }

    /// <summary>Reviews the combined output and returns a quality verdict.</summary>
    public async Task<QualityVerdict> ReviewAsync(
        string swarmId,
        string originalPrompt,
        string combinedOutput,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(swarmId);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPrompt);

        LogStarting(_logger, swarmId);

        var reviewPrompt = BuildReviewPrompt(originalPrompt, combinedOutput);

        var agent = _factory.Create(
            name: "swarm-quality-reviewer",
            role: AgentRole.Reviewer,
            customInstructions: reviewPrompt);

        var task = AgentTask.Create(reviewPrompt);
        var result = await agent.HandleAsync(task, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            LogParseFailed(_logger, swarmId, result.Error ?? "Agent returned failure");
            return new QualityVerdict(0, "error", result.Error ?? "Quality gate agent failed.");
        }

        var verdict = ParseVerdict(result.Output);

        if (verdict is null)
        {
            LogParseFailed(_logger, swarmId, "Could not parse verdict from output");
            return new QualityVerdict(5, "pass", "Quality gate completed but could not parse structured verdict.");
        }

        LogVerdict(_logger, swarmId, verdict.Verdict, verdict.Score);
        return verdict;
    }

    /// <summary>Parses the quality verdict from agent output (JSON or structured text).</summary>
    internal static QualityVerdict? ParseVerdict(string output)
    {
        // Try JSON first
        var json = ExtractJsonObject(output);
        if (json is not null)
        {
            try
            {
                return JsonSerializer.Deserialize<QualityVerdict>(json, s_jsonOptions);
            }
            catch (JsonException) { /* fall through to regex */ }
        }

        // Try structured text: SCORE: N, VERDICT: pass/fail/needs_revision, FEEDBACK: ...
        var scoreMatch = ScoreRegex().Match(output);
        var verdictMatch = VerdictRegex().Match(output);
        var feedbackMatch = FeedbackRegex().Match(output);

        if (scoreMatch.Success && verdictMatch.Success)
        {
            var score = int.Parse(scoreMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var verdict = verdictMatch.Groups[1].Value.Trim();
            var feedback = feedbackMatch.Success ? feedbackMatch.Groups[1].Value.Trim() : string.Empty;
            return new QualityVerdict(score, verdict, feedback);
        }

        return null;
    }

    private static string? ExtractJsonObject(string text)
    {
        // Try fenced code block
        var match = Regex.Match(text, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Try raw JSON object
        var start = text.IndexOf('{', StringComparison.Ordinal);
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];

        return null;
    }

    private static string BuildReviewPrompt(string originalPrompt, string combinedOutput)
    {
        return $"""
            You are a quality reviewer for a multi-agent swarm execution. Review the combined output
            from all tasks and assess whether the original goal was achieved.

            Original goal:
            {originalPrompt}

            Combined output from all tasks:
            {combinedOutput}

            Return ONLY a JSON object with:
            - "Score": integer 1-10 (10 = perfect)
            - "Verdict": one of "pass", "needs_revision", "fail"
            - "Feedback": brief explanation of the verdict

            Scoring guide:
            - 8-10: pass — goal fully achieved, quality is good
            - 5-7: needs_revision — partially achieved, some issues need fixing
            - 1-4: fail — goal not achieved or critical issues
            """;
    }

    [GeneratedRegex(@"SCORE:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ScoreRegex();

    [GeneratedRegex(@"VERDICT:\s*(pass|fail|needs_revision)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex VerdictRegex();

    [GeneratedRegex(@"FEEDBACK:\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FeedbackRegex();
}
