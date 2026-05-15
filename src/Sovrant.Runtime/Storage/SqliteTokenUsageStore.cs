namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed token usage store.
/// </summary>
internal sealed class SqliteTokenUsageStore(ISqliteConnectionFactory connectionFactory) : ITokenUsageStore
{
    public async Task RecordAsync(TokenUsageRecord record, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO token_usage (session_id, user_id, model, input_tokens, output_tokens, cost_usd)
            VALUES ($sid, $uid, $model, $in, $out, $cost)
            """;
        cmd.Parameters.AddWithValue("$sid", record.SessionId);
        cmd.Parameters.AddWithValue("$uid", Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName);
        cmd.Parameters.AddWithValue("$model", record.Model);
        cmd.Parameters.AddWithValue("$in", record.InputTokens);
        cmd.Parameters.AddWithValue("$out", record.OutputTokens);
        cmd.Parameters.AddWithValue("$cost", record.CostUsd.HasValue ? (object)record.CostUsd.Value : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<TokenUsageSummary> GetSessionTotalsAsync(string sessionId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(input_tokens), 0),
                   COALESCE(SUM(output_tokens), 0),
                   SUM(cost_usd)
            FROM token_usage
            WHERE session_id = $sid
            """;
        cmd.Parameters.AddWithValue("$sid", sessionId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new TokenUsageSummary(
                TotalInputTokens: reader.GetInt32(0),
                TotalOutputTokens: reader.GetInt32(1),
                TotalCostUsd: await reader.IsDBNullAsync(2, ct).ConfigureAwait(false) ? null : (decimal)reader.GetDouble(2));
        }

        return new TokenUsageSummary(0, 0, null);
    }
}
