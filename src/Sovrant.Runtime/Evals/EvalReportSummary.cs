using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Evals;

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
