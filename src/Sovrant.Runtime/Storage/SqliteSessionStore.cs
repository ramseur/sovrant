using System.Globalization;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed session store. Persists session entries to <c>sessions</c>
/// and <c>session_entries</c> tables with FTS5 auto-sync.
/// </summary>
internal sealed class SqliteSessionStore(ISqliteConnectionFactory connectionFactory) : ISessionStore
{
    public async Task AppendAsync(string sessionId, SessionEntry entry, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Ensure the session row exists.
        using var ensureCmd = connection.CreateCommand();
        ensureCmd.CommandText = """
            INSERT OR IGNORE INTO sessions (session_id, user_id, model, started_at, updated_at)
            VALUES ($sid, $uid, $model,
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        ensureCmd.Parameters.AddWithValue("$sid", sessionId);
        ensureCmd.Parameters.AddWithValue("$uid", Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName);
        ensureCmd.Parameters.AddWithValue("$model", (object?)entry.Model ?? DBNull.Value);
        await ensureCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Update the session timestamp.
        using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE sessions SET updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now') WHERE session_id = $sid";
        updateCmd.Parameters.AddWithValue("$sid", sessionId);
        await updateCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Insert the entry.
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO session_entries (session_id, entry_uid, timestamp, role, content, model,
                                         input_tokens, output_tokens, tool_name, tool_use_id, is_error)
            VALUES ($sid, $uid, $ts, $role, $content, $model, $in, $out, $tool, $tuid, $err)
            """;
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$uid", entry.Id);
        cmd.Parameters.AddWithValue("$ts", entry.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$role", entry.Role);
        cmd.Parameters.AddWithValue("$content", entry.Content);
        cmd.Parameters.AddWithValue("$model", (object?)entry.Model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$in", entry.InputTokens);
        cmd.Parameters.AddWithValue("$out", entry.OutputTokens);
        cmd.Parameters.AddWithValue("$tool", (object?)entry.ToolName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tuid", (object?)entry.ToolUseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$err", entry.IsError ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT entry_uid, timestamp, role, content, model, input_tokens, output_tokens,
                   tool_name, tool_use_id, is_error
            FROM session_entries
            WHERE session_id = $sid
            ORDER BY entry_id
            """;
        cmd.Parameters.AddWithValue("$sid", sessionId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var entries = new List<SessionEntry>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            entries.Add(new SessionEntry(
                Id: reader.GetString(0),
                Timestamp: DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                Role: reader.GetString(2),
                Content: reader.GetString(3))
            {
                Model = await reader.IsDBNullAsync(4, ct).ConfigureAwait(false) ? null : reader.GetString(4),
                InputTokens = reader.GetInt32(5),
                OutputTokens = reader.GetInt32(6),
                ToolName = await reader.IsDBNullAsync(7, ct).ConfigureAwait(false) ? null : reader.GetString(7),
                ToolUseId = await reader.IsDBNullAsync(8, ct).ConfigureAwait(false) ? null : reader.GetString(8),
                IsError = reader.GetInt32(9) != 0,
            });
        }

        return entries;
    }

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT session_id FROM sessions ORDER BY updated_at DESC";

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var ids = new List<string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            ids.Add(reader.GetString(0));

        return ids;
    }

    public async Task<bool> DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        using var delEntries = connection.CreateCommand();
        delEntries.CommandText = "DELETE FROM session_entries WHERE session_id = $sid";
        delEntries.Parameters.AddWithValue("$sid", sessionId);
        await delEntries.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        using var delSession = connection.CreateCommand();
        delSession.CommandText = "DELETE FROM sessions WHERE session_id = $sid";
        delSession.Parameters.AddWithValue("$sid", sessionId);
        var rows = await delSession.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return rows > 0;
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        using var delEntries = connection.CreateCommand();
        delEntries.CommandText = "DELETE FROM session_entries";
        await delEntries.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        using var delSessions = connection.CreateCommand();
        delSessions.CommandText = "DELETE FROM sessions";
        return await delSessions.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
