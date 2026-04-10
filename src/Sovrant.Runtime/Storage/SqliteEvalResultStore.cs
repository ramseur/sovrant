using System.Globalization;
using Sovrant.Runtime.Evals;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed eval result store. Persists eval reports to the
/// <c>eval_runs</c> and <c>eval_results</c> tables (V005 migration).
/// </summary>
internal sealed class SqliteEvalResultStore(ISqliteConnectionFactory connectionFactory) : IEvalResultStore
{
    public async Task SaveAsync(EvalReport report, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var connection = connectionFactory.CreateConnection();
        var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)
            await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        var runId = Guid.NewGuid().ToString("N");

        // Insert eval_runs row
        using var runCmd = connection.CreateCommand();
        runCmd.Transaction = transaction;
        runCmd.CommandText = """
            INSERT INTO eval_runs (run_id, suite_name, started_at, duration_seconds,
                                   pass_rate, pass_at_1_rate, total_passed, total_failed, total_skipped)
            VALUES ($runId, $suite, $started, $duration, $passRate, $passAt1, $passed, $failed, $skipped)
            """;
        runCmd.Parameters.AddWithValue("$runId", runId);
        runCmd.Parameters.AddWithValue("$suite", report.SuiteName);
        runCmd.Parameters.AddWithValue("$started", report.StartedAt.ToString("o", CultureInfo.InvariantCulture));
        runCmd.Parameters.AddWithValue("$duration", report.Duration.TotalSeconds);
        runCmd.Parameters.AddWithValue("$passRate", report.PassRate);
        runCmd.Parameters.AddWithValue("$passAt1", report.PassAt1Rate);
        runCmd.Parameters.AddWithValue("$passed", report.TotalPassed);
        runCmd.Parameters.AddWithValue("$failed", report.TotalFailed);
        runCmd.Parameters.AddWithValue("$skipped", report.TotalSkipped);
        await runCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Insert eval_results rows
        foreach (var result in report.Results)
        {
            using var resCmd = connection.CreateCommand();
            resCmd.Transaction = transaction;
            resCmd.CommandText = """
                INSERT INTO eval_results (run_id, eval_name, category, grader_type,
                                          passed, pass_at_1, pass_count, attempt_count,
                                          average_score, duration_seconds, skipped)
                VALUES ($runId, $name, $cat, $grader, $passed, $passAt1, $passCount,
                        $attempts, $avgScore, $duration, $skipped)
                """;
            resCmd.Parameters.AddWithValue("$runId", runId);
            resCmd.Parameters.AddWithValue("$name", result.EvalName);
            resCmd.Parameters.AddWithValue("$cat", result.Category.ToString());
            resCmd.Parameters.AddWithValue("$grader", result.GraderType.ToString());
            resCmd.Parameters.AddWithValue("$passed", result.Passed ? 1 : 0);
            resCmd.Parameters.AddWithValue("$passAt1", result.PassAt1 ? 1 : 0);
            resCmd.Parameters.AddWithValue("$passCount", result.PassCount);
            resCmd.Parameters.AddWithValue("$attempts", result.Attempts.Count);
            resCmd.Parameters.AddWithValue("$avgScore", result.AverageScore.HasValue ? result.AverageScore.Value : DBNull.Value);
            resCmd.Parameters.AddWithValue("$duration", result.TotalDuration.TotalSeconds);
            resCmd.Parameters.AddWithValue("$skipped", result.Skipped ? 1 : 0);
            await resCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public IReadOnlyList<EvalReportSummary> LoadHistory(string suiteName, int maxResults = 50)
    {
        using var connection = connectionFactory.CreateReadOnlyConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT suite_name, started_at, duration_seconds,
                   pass_rate, pass_at_1_rate, total_passed, total_failed, total_skipped
            FROM eval_runs
            WHERE suite_name = $suite
            ORDER BY started_at DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$suite", suiteName);
        cmd.Parameters.AddWithValue("$limit", maxResults);

        using var reader = cmd.ExecuteReader();
        var results = new List<EvalReportSummary>();
        while (reader.Read())
        {
            results.Add(new EvalReportSummary
            {
                SuiteName = reader.GetString(0),
                StartedAt = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                DurationSeconds = reader.GetDouble(2),
                PassRate = reader.GetDouble(3),
                PassAt1Rate = reader.GetDouble(4),
                TotalPassed = reader.GetInt32(5),
                TotalFailed = reader.GetInt32(6),
                TotalSkipped = reader.GetInt32(7),
            });
        }

        return results;
    }
}
