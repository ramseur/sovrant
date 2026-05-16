using System.Globalization;
using System.Text.Json;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed session store. Persists session entries to <c>sessions</c>
/// and <c>session_entries</c> tables with FTS5 auto-sync.
///
/// <para>Phase 38 — enforces per-user ownership via the <c>sessions.user_id</c>
/// column. When <c>ownerUserId</c> is passed to read/delete methods, queries
/// are scoped to that user. When null, no filter is applied (admin/system).</para>
/// </summary>
internal sealed class SqliteSessionStore(ISqliteConnectionFactory connectionFactory) : ISessionStore
{
    /// <summary>
    /// Fallback owner recorded on session rows created from a code path that
    /// did not supply an authenticated user id (e.g. the CLI, tests, or
    /// pre-Phase-38 callers). Matches the seeded default user so legacy
    /// admin callers keep working.
    /// </summary>
    private static string LegacyOwner =>
        Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName;

    public async Task AppendAsync(
        string sessionId,
        SessionEntry entry,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Ensure the session row exists. INSERT OR IGNORE means the owner is
        // only written on first-touch — later appends cannot overwrite the
        // recorded owner, which is what makes ownership stable.
        using var ensureCmd = connection.CreateCommand();
        ensureCmd.CommandText = """
            INSERT OR IGNORE INTO sessions (session_id, user_id, model, started_at, updated_at)
            VALUES ($sid, $uid, $model,
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        ensureCmd.Parameters.AddWithValue("$sid", sessionId);
        ensureCmd.Parameters.AddWithValue("$uid", ownerUserId ?? LegacyOwner);
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
            INSERT INTO session_entries (session_id, entry_uid, timestamp, role, content, model, provider,
                                         input_tokens, output_tokens, tool_name, tool_use_id, is_error)
            VALUES ($sid, $uid, $ts, $role, $content, $model, $provider, $in, $out, $tool, $tuid, $err)
            """;
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$uid", entry.Id);
        cmd.Parameters.AddWithValue("$ts", entry.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$role", entry.Role);
        cmd.Parameters.AddWithValue("$content", entry.Content);
        cmd.Parameters.AddWithValue("$model", (object?)entry.Model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$provider", (object?)entry.Provider ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$in", entry.InputTokens);
        cmd.Parameters.AddWithValue("$out", entry.OutputTokens);
        cmd.Parameters.AddWithValue("$tool", (object?)entry.ToolName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tuid", (object?)entry.ToolUseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$err", entry.IsError ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SessionEntry>> LoadAsync(
        string sessionId,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Pre-flight ownership check — cheap and avoids leaking whether a
        // session exists across users via row-count side channels.
        if (ownerUserId is not null)
        {
            using var ownerCheck = connection.CreateCommand();
            ownerCheck.CommandText = "SELECT user_id FROM sessions WHERE session_id = $sid";
            ownerCheck.Parameters.AddWithValue("$sid", sessionId);
            var owner = await ownerCheck.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
            if (owner is null || !string.Equals(owner, ownerUserId, StringComparison.Ordinal))
                return [];
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT entry_uid, timestamp, role, content, model, provider, input_tokens, output_tokens,
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
                Provider = await reader.IsDBNullAsync(5, ct).ConfigureAwait(false) ? null : reader.GetString(5),
                InputTokens = reader.GetInt32(6),
                OutputTokens = reader.GetInt32(7),
                ToolName = await reader.IsDBNullAsync(8, ct).ConfigureAwait(false) ? null : reader.GetString(8),
                ToolUseId = await reader.IsDBNullAsync(9, ct).ConfigureAwait(false) ? null : reader.GetString(9),
                IsError = reader.GetInt32(10) != 0,
            });
        }

        return entries;
    }

    public async Task<IReadOnlyList<string>> ListAsync(
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        if (ownerUserId is null)
        {
            cmd.CommandText = "SELECT session_id FROM sessions ORDER BY updated_at DESC";
        }
        else
        {
            cmd.CommandText = "SELECT session_id FROM sessions WHERE user_id = $uid ORDER BY updated_at DESC";
            cmd.Parameters.AddWithValue("$uid", ownerUserId);
        }

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var ids = new List<string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            ids.Add(reader.GetString(0));

        return ids;
    }

    public async Task<bool> DeleteAsync(
        string sessionId,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        if (ownerUserId is not null)
        {
            using var ownerCheck = connection.CreateCommand();
            ownerCheck.CommandText = "SELECT user_id FROM sessions WHERE session_id = $sid";
            ownerCheck.Parameters.AddWithValue("$sid", sessionId);
            var owner = await ownerCheck.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
            if (owner is null || !string.Equals(owner, ownerUserId, StringComparison.Ordinal))
                return false;
        }

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

    public async Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT user_id FROM sessions WHERE session_id = $sid";
        cmd.Parameters.AddWithValue("$sid", sessionId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result as string;
    }

    public async Task SetTitleAsync(string sessionId, string title, string? ownerUserId = null, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        if (ownerUserId is not null)
        {
            // Upsert the session row first so that SetTitleAsync can be called
            // before the first AppendAsync (e.g. to show the session in the sidebar
            // immediately when the user sends their first message).
            using var ensureCmd = connection.CreateCommand();
            ensureCmd.CommandText = """
                INSERT OR IGNORE INTO sessions (session_id, user_id, started_at, updated_at)
                VALUES ($sid, $uid, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'), strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                """;
            ensureCmd.Parameters.AddWithValue("$sid", sessionId);
            ensureCmd.Parameters.AddWithValue("$uid", ownerUserId);
            await ensureCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                UPDATE sessions SET title = $title, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                WHERE session_id = $sid AND user_id = $uid
                """;
            cmd.Parameters.AddWithValue("$sid", sessionId);
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$uid", ownerUserId);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        else
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE sessions SET title = $title WHERE session_id = $sid";
            cmd.Parameters.AddWithValue("$sid", sessionId);
            cmd.Parameters.AddWithValue("$title", title);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<string?> GetTitleAsync(string sessionId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT title FROM sessions WHERE session_id = $sid";
        cmd.Parameters.AddWithValue("$sid", sessionId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result as string;
    }

    public async Task<IReadOnlyList<SessionListItem>> ListWithTitlesAsync(string? ownerUserId = null, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        if (ownerUserId is null)
        {
            cmd.CommandText = "SELECT session_id, title, updated_at FROM sessions ORDER BY updated_at DESC";
        }
        else
        {
            cmd.CommandText = "SELECT session_id, title, updated_at FROM sessions WHERE user_id = $uid ORDER BY updated_at DESC";
            cmd.Parameters.AddWithValue("$uid", ownerUserId);
        }

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var summaries = new List<SessionListItem>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            summaries.Add(new SessionListItem(
                SessionId: reader.GetString(0),
                Title: await reader.IsDBNullAsync(1, ct).ConfigureAwait(false) ? null : reader.GetString(1),
                UpdatedAt: DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
        }
        return summaries;
    }

    public async Task<IReadOnlyList<string>?> GetMcpConnectionsAsync(string sessionId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT mcp_servers FROM sessions WHERE session_id = $sid";
        cmd.Parameters.AddWithValue("$sid", sessionId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (result is null || result is DBNull) return null;
        var json = (string)result;
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SetMcpConnectionsAsync(
        string sessionId,
        IReadOnlyList<string>? servers,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        if (ownerUserId is not null)
        {
            using var ownerCheck = connection.CreateCommand();
            ownerCheck.CommandText = "SELECT user_id FROM sessions WHERE session_id = $sid";
            ownerCheck.Parameters.AddWithValue("$sid", sessionId);
            var owner = await ownerCheck.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
            if (owner is null || !string.Equals(owner, ownerUserId, StringComparison.Ordinal))
                return;
        }

        // Ensure a session row exists so the UI can record the gate before any
        // entries have been appended.
        using var ensureCmd = connection.CreateCommand();
        ensureCmd.CommandText = """
            INSERT OR IGNORE INTO sessions (session_id, user_id, started_at, updated_at)
            VALUES ($sid, $uid,
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        ensureCmd.Parameters.AddWithValue("$sid", sessionId);
        ensureCmd.Parameters.AddWithValue("$uid", ownerUserId ?? LegacyOwner);
        await ensureCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE sessions SET mcp_servers = $mcp WHERE session_id = $sid";
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$mcp", servers is null ? DBNull.Value : JsonSerializer.Serialize(servers));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SessionListItem>> SearchAsync(
        string query,
        string? ownerUserId = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();

        // Use FTS5 MATCH to search session entry content, join back to sessions
        // for title and ownership. Group by session so each session appears once.
        if (ownerUserId is null)
        {
            cmd.CommandText = """
                SELECT s.session_id, s.title, s.updated_at
                FROM session_entries_fts f
                JOIN session_entries e ON e.entry_id = f.rowid
                JOIN sessions s ON s.session_id = e.session_id
                WHERE session_entries_fts MATCH $query
                GROUP BY s.session_id
                ORDER BY rank
                LIMIT $limit
                """;
        }
        else
        {
            cmd.CommandText = """
                SELECT s.session_id, s.title, s.updated_at
                FROM session_entries_fts f
                JOIN session_entries e ON e.entry_id = f.rowid
                JOIN sessions s ON s.session_id = e.session_id
                WHERE session_entries_fts MATCH $query AND s.user_id = $uid
                GROUP BY s.session_id
                ORDER BY rank
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$uid", ownerUserId);
        }

        cmd.Parameters.AddWithValue("$query", query);
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<SessionListItem>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new SessionListItem(
                SessionId: reader.GetString(0),
                Title: await reader.IsDBNullAsync(1, ct).ConfigureAwait(false) ? null : reader.GetString(1),
                UpdatedAt: DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture)));
        }
        return results;
    }
}
