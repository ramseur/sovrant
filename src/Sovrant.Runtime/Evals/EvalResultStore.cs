using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Evals;

/// <summary>
/// Persists eval reports to <c>~/.sovrant/evals/results/</c> as JSON files
/// for trend tracking over time.
/// </summary>
public sealed class EvalResultStore : IEvalResultStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _resultsDir;

    public EvalResultStore(string? resultsDir = null)
    {
        _resultsDir = resultsDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "evals", "results");
    }

    /// <summary>Saves an eval report to disk.</summary>
    public async Task SaveAsync(EvalReport report, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(_resultsDir);

        var timestamp = report.StartedAt.ToString("yyyy-MM-dd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"{report.SuiteName}_{timestamp}.json";
        var filePath = Path.Combine(_resultsDir, fileName);

        var dto = ToDto(report);
        var json = JsonSerializer.Serialize(dto, s_jsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct).ConfigureAwait(false);
    }

    /// <summary>Loads all historical reports for a given suite name (most recent first).</summary>
    public IReadOnlyList<EvalReportSummary> LoadHistory(string suiteName, int maxResults = 50)
    {
        if (!Directory.Exists(_resultsDir))
            return [];

        var prefix = suiteName + "_";
        return Directory.GetFiles(_resultsDir, $"{prefix}*.json")
            .OrderByDescending(f => f)
            .Take(maxResults)
            .Select(f =>
            {
                try
                {
                    var json = File.ReadAllText(f);
                    return JsonSerializer.Deserialize<EvalReportSummary>(json, s_jsonOptions);
                }
                catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException) { return null; }
            })
            .Where(r => r is not null)
            .ToList()!;
    }

    private static EvalReportDto ToDto(EvalReport report) => new()
    {
        SuiteName = report.SuiteName,
        StartedAt = report.StartedAt,
        DurationSeconds = report.Duration.TotalSeconds,
        PassRate = report.PassRate,
        PassAt1Rate = report.PassAt1Rate,
        TotalPassed = report.TotalPassed,
        TotalFailed = report.TotalFailed,
        TotalSkipped = report.TotalSkipped,
        Results = report.Results.Select(r => new EvalResultDto
        {
            EvalName = r.EvalName,
            Category = r.Category,
            GraderType = r.GraderType,
            Passed = r.Passed,
            PassAt1 = r.PassAt1,
            PassCount = r.PassCount,
            AttemptCount = r.Attempts.Count,
            AverageScore = r.AverageScore,
            DurationSeconds = r.TotalDuration.TotalSeconds,
            Skipped = r.Skipped,
        }).ToList(),
    };
}

/// <summary>Serializable summary for trend tracking.</summary>
public class EvalReportSummary
{
    [JsonPropertyName("suite_name")] public string SuiteName { get; init; } = string.Empty;
    [JsonPropertyName("started_at")] public DateTimeOffset StartedAt { get; init; }
    [JsonPropertyName("duration_seconds")] public double DurationSeconds { get; init; }
    [JsonPropertyName("pass_rate")] public double PassRate { get; init; }
    [JsonPropertyName("pass_at_1_rate")] public double PassAt1Rate { get; init; }
    [JsonPropertyName("total_passed")] public int TotalPassed { get; init; }
    [JsonPropertyName("total_failed")] public int TotalFailed { get; init; }
    [JsonPropertyName("total_skipped")] public int TotalSkipped { get; init; }
}

internal sealed class EvalReportDto : EvalReportSummary
{
    [JsonPropertyName("results")] public IReadOnlyList<EvalResultDto> Results { get; init; } = [];
}

internal sealed class EvalResultDto
{
    [JsonPropertyName("eval_name")] public string EvalName { get; init; } = string.Empty;
    [JsonPropertyName("category")] public EvalCategory Category { get; init; }
    [JsonPropertyName("grader_type")] public GraderType GraderType { get; init; }
    [JsonPropertyName("passed")] public bool Passed { get; init; }
    [JsonPropertyName("pass_at_1")] public bool PassAt1 { get; init; }
    [JsonPropertyName("pass_count")] public int PassCount { get; init; }
    [JsonPropertyName("attempt_count")] public int AttemptCount { get; init; }
    [JsonPropertyName("average_score")] public double? AverageScore { get; init; }
    [JsonPropertyName("duration_seconds")] public double DurationSeconds { get; init; }
    [JsonPropertyName("skipped")] public bool Skipped { get; init; }
}
