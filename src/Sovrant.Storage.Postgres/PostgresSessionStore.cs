using System.Globalization;
using System.Text.Json;
using Npgsql;
using Sovrant.Runtime.Session;

namespace Sovrant.Storage.Postgres;

internal sealed class PostgresSessionStore(IPostgresConnectionFactory factory) : ISessionStore
{
    private const string UtcNow =
        "to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.MS\"Z\"')";

    public async Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();

        // New conversations are private by default, matching SqliteSessionStore (the column's
        // DEFAULT 0 only exists for rows that predate V030).
        using var ensureCmd = conn.CreateCommand();
        ensureCmd.CommandText = $"""
            INSERT INTO sessions (session_id, user_id, model, started_at, updated_at, is_private)
            VALUES ($1, $2, $3, {UtcNow}, {UtcNow}, 1)
            ON CONFLICT (session_id) DO UPDATE SET updated_at = {UtcNow}
            """;
        ensureCmd.Parameters.AddWithValue(sessionId);
        ensureCmd.Parameters.AddWithValue(ownerUserId ?? Environment.UserName);
        ensureCmd.Parameters.AddWithValue((object?)entry.Model ?? DBNull.Value);
        await ensureCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO session_entries
                (session_id, entry_uid, timestamp, role, content, model, provider,
                 input_tokens, output_tokens, tool_name, tool_use_id, is_error)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            """;
        cmd.Parameters.AddWithValue(sessionId);
        cmd.Parameters.AddWithValue(entry.Id);
        cmd.Parameters.AddWithValue(entry.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue(entry.Role);
        cmd.Parameters.AddWithValue(entry.Content);
        cmd.Parameters.AddWithValue((object?)entry.Model ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)entry.Provider ?? DBNull.Value);
        cmd.Parameters.AddWithValue(entry.InputTokens);
        cmd.Parameters.AddWithValue(entry.OutputTokens);
        cmd.Parameters.AddWithValue((object?)entry.ToolName ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)entry.ToolUseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(entry.IsError ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();

        if (ownerUserId is not null)
        {
            using var ownerCmd = conn.CreateCommand();
            ownerCmd.CommandText = "SELECT user_id FROM sessions WHERE session_id = $1";
            ownerCmd.Parameters.AddWithValue(sessionId);
            var owner = await ownerCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
            if (owner is null || !string.Equals(owner, ownerUserId, StringComparison.Ordinal))
                return [];
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT entry_uid, timestamp, role, content, model, provider, input_tokens, output_tokens,
                   tool_name, tool_use_id, is_error
            FROM session_entries WHERE session_id = $1 ORDER BY entry_id
            """;
        cmd.Parameters.AddWithValue(sessionId);
        return await ReadEntriesAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        if (ownerUserId is null)
            cmd.CommandText = "SELECT session_id FROM sessions ORDER BY updated_at DESC";
        else
        {
            cmd.CommandText = "SELECT session_id FROM sessions WHERE user_id = $1 ORDER BY updated_at DESC";
            cmd.Parameters.AddWithValue(ownerUserId);
        }
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var ids = new List<string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            ids.Add(reader.GetString(0));
        return ids;
    }

    public async Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        if (ownerUserId is null)
        {
            cmd.CommandText = "DELETE FROM sessions WHERE session_id = $1";
            cmd.Parameters.AddWithValue(sessionId);
        }
        else
        {
            cmd.CommandText = "DELETE FROM sessions WHERE session_id = $1 AND user_id = $2";
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(ownerUserId);
        }
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions";
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT user_id FROM sessions WHERE session_id = $1";
        cmd.Parameters.AddWithValue(sessionId);
        return await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }

    public async Task SetTitleAsync(string sessionId, string title, string? ownerUserId = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        if (ownerUserId is null)
        {
            cmd.CommandText = $"UPDATE sessions SET title = $1, updated_at = {UtcNow} WHERE session_id = $2";
            cmd.Parameters.AddWithValue(title);
            cmd.Parameters.AddWithValue(sessionId);
        }
        else
        {
            cmd.CommandText = $"UPDATE sessions SET title = $1, updated_at = {UtcNow} WHERE session_id = $2 AND user_id = $3";
            cmd.Parameters.AddWithValue(title);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(ownerUserId);
        }
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<string?> GetTitleAsync(string sessionId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT title FROM sessions WHERE session_id = $1";
        cmd.Parameters.AddWithValue(sessionId);
        return await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }

    public async Task<IReadOnlyList<SessionListItem>> ListWithTitlesAsync(string? ownerUserId = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        if (ownerUserId is null)
            cmd.CommandText = "SELECT session_id, title, updated_at, user_id, workspace_id, is_private, folder_id, agent_name FROM sessions ORDER BY updated_at DESC";
        else
        {
            cmd.CommandText = "SELECT session_id, title, updated_at, user_id, workspace_id, is_private, folder_id, agent_name FROM sessions WHERE user_id = $1 ORDER BY updated_at DESC";
            cmd.Parameters.AddWithValue(ownerUserId);
        }
        return await ReadSessionItemsAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SessionListItem>> SearchAsync(string query, string? ownerUserId = null, int limit = 50, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.Parameters.AddWithValue(query);
        cmd.Parameters.AddWithValue(limit);

        if (ownerUserId is null)
        {
            cmd.CommandText = """
                SELECT DISTINCT s.session_id, s.title, s.updated_at
                FROM sessions s
                JOIN session_entries e ON e.session_id = s.session_id
                WHERE e.search_vector @@ plainto_tsquery('english', $1)
                ORDER BY s.updated_at DESC LIMIT $2
                """;
        }
        else
        {
            cmd.CommandText = """
                SELECT DISTINCT s.session_id, s.title, s.updated_at
                FROM sessions s
                JOIN session_entries e ON e.session_id = s.session_id
                WHERE e.search_vector @@ plainto_tsquery('english', $1) AND s.user_id = $3
                ORDER BY s.updated_at DESC LIMIT $2
                """;
            cmd.Parameters.AddWithValue(ownerUserId);
        }

        return await ReadSessionItemsAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>?> GetMcpConnectionsAsync(string sessionId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT mcp_servers FROM sessions WHERE session_id = $1";
        cmd.Parameters.AddWithValue(sessionId);
        var raw = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
        if (raw is null) return null;
        return JsonSerializer.Deserialize<List<string>>(raw);
    }

    public async Task UpdatePrivacyAsync(string sessionId, string ownerUserId, bool isPrivate, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE sessions SET is_private = $1 WHERE session_id = $2 AND user_id = $3";
        // is_private is INTEGER (0/1) in the schema, as on SQLite; Postgres won't store a boolean in it.
        cmd.Parameters.AddWithValue(isPrivate ? 1 : 0);
        cmd.Parameters.AddWithValue(sessionId);
        cmd.Parameters.AddWithValue(ownerUserId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool?> GetIsPrivateAsync(string sessionId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT is_private FROM sessions WHERE session_id = $1";
        cmd.Parameters.AddWithValue(sessionId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (result is null || result is DBNull) return null;
        return Convert.ToInt64(result, CultureInfo.InvariantCulture) != 0; // INTEGER column, not boolean
    }

    public async Task SetMcpConnectionsAsync(string sessionId, IReadOnlyList<string>? servers, string? ownerUserId = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        var value = servers is null ? (object)DBNull.Value : JsonSerializer.Serialize(servers);
        if (ownerUserId is null)
        {
            cmd.CommandText = "UPDATE sessions SET mcp_servers = $1 WHERE session_id = $2";
            cmd.Parameters.AddWithValue(value);
            cmd.Parameters.AddWithValue(sessionId);
        }
        else
        {
            cmd.CommandText = "UPDATE sessions SET mcp_servers = $1 WHERE session_id = $2 AND user_id = $3";
            cmd.Parameters.AddWithValue(value);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(ownerUserId);
        }
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<List<SessionEntry>> ReadEntriesAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
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
                Model    = await reader.IsDBNullAsync(4, ct).ConfigureAwait(false) ? null : reader.GetString(4),
                Provider = await reader.IsDBNullAsync(5, ct).ConfigureAwait(false) ? null : reader.GetString(5),
                InputTokens  = reader.GetInt32(6),
                OutputTokens = reader.GetInt32(7),
                ToolName  = await reader.IsDBNullAsync(8, ct).ConfigureAwait(false)  ? null : reader.GetString(8),
                ToolUseId = await reader.IsDBNullAsync(9, ct).ConfigureAwait(false)  ? null : reader.GetString(9),
                IsError   = reader.GetInt32(10) != 0,
            });
        }
        return entries;
    }

    private static async Task<List<SessionListItem>> ReadSessionItemsAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var items = new List<SessionListItem>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            // Search selects only session_id, title and updated_at; the list adds owner, workspace,
            // privacy, folder and agent (the same columns as SqliteSessionStore). Columns are read by
            // name so a query that omits one can't shift the others: the original reader went by
            // position and never read is_private, so every Postgres row looked public (Phase 134).
            items.Add(new SessionListItem(
                SessionId: reader.GetString(0),
                Title: await reader.IsDBNullAsync(1, ct).ConfigureAwait(false) ? null : reader.GetString(1),
                UpdatedAt: DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                OwnerUserId: await OptionalStringAsync(reader, "user_id", ct).ConfigureAwait(false),
                WorkspaceId: await OptionalStringAsync(reader, "workspace_id", ct).ConfigureAwait(false),
                IsPrivate: await OptionalFlagAsync(reader, "is_private", ct).ConfigureAwait(false),
                FolderId: await OptionalStringAsync(reader, "folder_id", ct).ConfigureAwait(false),
                AgentName: await OptionalStringAsync(reader, "agent_name", ct).ConfigureAwait(false)));
        }
        return items;
    }

    private static async Task<string?> OptionalStringAsync(NpgsqlDataReader reader, string column, CancellationToken ct) =>
        Ordinal(reader, column) is { } i && !await reader.IsDBNullAsync(i, ct).ConfigureAwait(false)
            ? reader.GetString(i)
            : null;

    private static async Task<bool> OptionalFlagAsync(NpgsqlDataReader reader, string column, CancellationToken ct) =>
        Ordinal(reader, column) is { } i
        && !await reader.IsDBNullAsync(i, ct).ConfigureAwait(false)
        && Convert.ToInt64(reader.GetValue(i), CultureInfo.InvariantCulture) != 0;

    private static int? Ordinal(NpgsqlDataReader reader, string column)
    {
        for (var i = 0; i < reader.FieldCount; i++)
            if (string.Equals(reader.GetName(i), column, StringComparison.Ordinal))
                return i;
        return null;
    }

    public async Task SetAgentNameAsync(string sessionId, string agentName, string? ownerUserId = null, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        if (ownerUserId is null)
        {
            cmd.CommandText = "UPDATE sessions SET agent_name = $1 WHERE session_id = $2";
            cmd.Parameters.AddWithValue(agentName);
            cmd.Parameters.AddWithValue(sessionId);
        }
        else
        {
            cmd.CommandText = "UPDATE sessions SET agent_name = $1 WHERE session_id = $2 AND user_id = $3";
            cmd.Parameters.AddWithValue(agentName);
            cmd.Parameters.AddWithValue(sessionId);
            cmd.Parameters.AddWithValue(ownerUserId);
        }
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<string?> GetAgentNameAsync(string sessionId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT agent_name FROM sessions WHERE session_id = $1";
        cmd.Parameters.AddWithValue(sessionId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (result is null || result is DBNull) return null;
        return (string)result;
    }
}
