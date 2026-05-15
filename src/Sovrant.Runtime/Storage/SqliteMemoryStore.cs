using System.Globalization;
using System.Text.Json;
using Sovrant.Runtime.Memory;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed memory store for session summaries, learned patterns, and instincts.
/// </summary>
internal sealed class SqliteMemoryStore(ISqliteConnectionFactory connectionFactory) : IMemoryStore
{
    private static readonly JsonSerializerOptions s_json = new() { WriteIndented = false };

    // ── Session Summaries ──────────────────────────────────────────────────

    public async Task SaveSummaryAsync(SessionSummary summary, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO session_summaries
                (session_id, project, started_at, ended_at, tasks, tools_used, files_modified,
                 outcome, total_input_tokens, total_output_tokens, turn_count, error_count)
            VALUES ($sid, $proj, $start, $end, $tasks, $tools, $files,
                    $outcome, $in, $out, $turns, $errors)
            """;
        cmd.Parameters.AddWithValue("$sid", summary.SessionId);
        cmd.Parameters.AddWithValue("$proj", summary.Project);
        cmd.Parameters.AddWithValue("$start", summary.StartedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$end", summary.EndedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$tasks", JsonSerializer.Serialize(summary.Tasks, s_json));
        cmd.Parameters.AddWithValue("$tools", JsonSerializer.Serialize(summary.ToolsUsed, s_json));
        cmd.Parameters.AddWithValue("$files", JsonSerializer.Serialize(summary.FilesModified, s_json));
        cmd.Parameters.AddWithValue("$outcome", summary.Outcome.ToString());
        cmd.Parameters.AddWithValue("$in", summary.TotalInputTokens);
        cmd.Parameters.AddWithValue("$out", summary.TotalOutputTokens);
        cmd.Parameters.AddWithValue("$turns", summary.TurnCount);
        cmd.Parameters.AddWithValue("$errors", summary.ErrorCount);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SessionSummary>> LoadSummariesAsync(string project, int maxCount = 5, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, project, started_at, ended_at, tasks, tools_used, files_modified,
                   outcome, total_input_tokens, total_output_tokens, turn_count, error_count
            FROM session_summaries
            WHERE project = $proj
            ORDER BY ended_at DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$proj", project);
        cmd.Parameters.AddWithValue("$limit", maxCount);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<SessionSummary>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new SessionSummary
            {
                SessionId = reader.GetString(0),
                Project = reader.GetString(1),
                StartedAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                EndedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                Tasks = JsonSerializer.Deserialize<List<string>>(reader.GetString(4)) ?? [],
                ToolsUsed = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? [],
                FilesModified = JsonSerializer.Deserialize<List<string>>(reader.GetString(6)) ?? [],
                Outcome = Enum.TryParse<SessionOutcome>(reader.GetString(7), out var o) ? o : SessionOutcome.Unknown,
                TotalInputTokens = reader.GetInt32(8),
                TotalOutputTokens = reader.GetInt32(9),
                TurnCount = reader.GetInt32(10),
                ErrorCount = reader.GetInt32(11),
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<SessionSummary>> LoadSummariesAsync(string project, string workspaceId, int maxCount = 5, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id, project, started_at, ended_at, tasks, tools_used, files_modified,
                   outcome, total_input_tokens, total_output_tokens, turn_count, error_count
            FROM session_summaries
            WHERE project = $proj AND workspace_id = $wid
            ORDER BY ended_at DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$proj", project);
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        cmd.Parameters.AddWithValue("$limit", maxCount);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<SessionSummary>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new SessionSummary
            {
                SessionId = reader.GetString(0),
                Project = reader.GetString(1),
                StartedAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                EndedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                Tasks = JsonSerializer.Deserialize<List<string>>(reader.GetString(4)) ?? [],
                ToolsUsed = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? [],
                FilesModified = JsonSerializer.Deserialize<List<string>>(reader.GetString(6)) ?? [],
                Outcome = Enum.TryParse<SessionOutcome>(reader.GetString(7), out var o) ? o : SessionOutcome.Unknown,
                TotalInputTokens = reader.GetInt32(8),
                TotalOutputTokens = reader.GetInt32(9),
                TurnCount = reader.GetInt32(10),
                ErrorCount = reader.GetInt32(11),
            });
        }

        return results;
    }

    // ── Learned Patterns ───────────────────────────────────────────────────

    public async Task SavePatternAsync(LearnedPattern pattern, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO learned_patterns (id, pattern, project, source_session, confidence, created_at, last_used)
            VALUES ($id, $pattern, $proj, $src, $conf, $created, $used)
            """;
        cmd.Parameters.AddWithValue("$id", pattern.Id);
        cmd.Parameters.AddWithValue("$pattern", pattern.Pattern);
        cmd.Parameters.AddWithValue("$proj", pattern.Project);
        cmd.Parameters.AddWithValue("$src", (object?)pattern.SourceSession ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$conf", pattern.Confidence);
        cmd.Parameters.AddWithValue("$created", pattern.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$used", pattern.LastUsed.ToString("o", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LearnedPattern>> LoadPatternsAsync(string project, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, pattern, project, source_session, confidence, created_at, last_used
            FROM learned_patterns
            WHERE project = $proj
            ORDER BY confidence DESC
            """;
        cmd.Parameters.AddWithValue("$proj", project);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<LearnedPattern>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new LearnedPattern
            {
                Id = reader.GetString(0),
                Pattern = reader.GetString(1),
                Project = reader.GetString(2),
                SourceSession = await reader.IsDBNullAsync(3, ct).ConfigureAwait(false) ? null : reader.GetString(3),
                Confidence = reader.GetDouble(4),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                LastUsed = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<LearnedPattern>> LoadPatternsAsync(string project, string workspaceId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT lp.id, lp.pattern, lp.project, lp.source_session, lp.confidence, lp.created_at, lp.last_used
            FROM learned_patterns lp
            INNER JOIN session_summaries ss ON lp.source_session = ss.session_id
            WHERE lp.project = $proj AND ss.workspace_id = $wid
            ORDER BY lp.confidence DESC
            """;
        cmd.Parameters.AddWithValue("$proj", project);
        cmd.Parameters.AddWithValue("$wid", workspaceId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<LearnedPattern>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new LearnedPattern
            {
                Id = reader.GetString(0),
                Pattern = reader.GetString(1),
                Project = reader.GetString(2),
                SourceSession = await reader.IsDBNullAsync(3, ct).ConfigureAwait(false) ? null : reader.GetString(3),
                Confidence = reader.GetDouble(4),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                LastUsed = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
            });
        }

        return results;
    }

    public async Task RemovePatternAsync(string id, string project, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM learned_patterns WHERE id = $id AND project = $proj";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$proj", project);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    // ── Instincts ──────────────────────────────────────────────────────────

    public async Task SaveInstinctAsync(Instinct instinct, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO instincts (id, trigger, action, confidence, evidence, created_at, updated_at)
            VALUES ($id, $trigger, $action, $conf, $evidence, $created, $updated)
            """;
        cmd.Parameters.AddWithValue("$id", instinct.Id);
        cmd.Parameters.AddWithValue("$trigger", instinct.Trigger);
        cmd.Parameters.AddWithValue("$action", instinct.Action);
        cmd.Parameters.AddWithValue("$conf", instinct.Confidence);
        cmd.Parameters.AddWithValue("$evidence", JsonSerializer.Serialize(instinct.Evidence, s_json));
        cmd.Parameters.AddWithValue("$created", instinct.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$updated", instinct.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Instinct>> LoadInstinctsAsync(double minConfidence = Instinct.PruneThreshold, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, trigger, action, confidence, evidence, created_at, updated_at
            FROM instincts
            WHERE confidence >= $min
            ORDER BY confidence DESC
            """;
        cmd.Parameters.AddWithValue("$min", minConfidence);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<Instinct>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new Instinct
            {
                Id = reader.GetString(0),
                Trigger = reader.GetString(1),
                Action = reader.GetString(2),
                Confidence = reader.GetDouble(3),
                Evidence = JsonSerializer.Deserialize<List<string>>(reader.GetString(4)) ?? [],
                CreatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
            });
        }

        return results;
    }

    public async Task ReinforceInstinctAsync(string id, string evidence, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE instincts
            SET confidence = MIN(confidence + $delta, 1.0),
                evidence = json_insert(evidence, '$[#]', $ev),
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$delta", Instinct.ReinforcementDelta);
        cmd.Parameters.AddWithValue("$ev", evidence);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task CorrectInstinctAsync(string id, string evidence, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Update confidence and add evidence.
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE instincts
            SET confidence = MAX(confidence - $delta, 0.0),
                evidence = json_insert(evidence, '$[#]', $ev),
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$delta", Instinct.CorrectionDelta);
        cmd.Parameters.AddWithValue("$ev", evidence);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Prune if below threshold.
        using var prune = connection.CreateCommand();
        prune.CommandText = "DELETE FROM instincts WHERE id = $id AND confidence < $min";
        prune.Parameters.AddWithValue("$id", id);
        prune.Parameters.AddWithValue("$min", Instinct.PruneThreshold);
        await prune.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveInstinctAsync(string id, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM instincts WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> PruneInstinctsAsync(double threshold = Instinct.PruneThreshold, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM instincts WHERE confidence < $threshold";
        cmd.Parameters.AddWithValue("$threshold", threshold);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
