using Microsoft.Data.Sqlite;
using Npgsql;

namespace Sovrant.Storage.Postgres;

/// <summary>
/// Copies sessions, session_entries, and credentials from a local SQLite database
/// into the Postgres target. All inserts use ON CONFLICT DO NOTHING so the operation
/// is idempotent — safe to run multiple times or resume after interruption.
/// </summary>
public sealed class SqliteToPostgresMigrator(IPostgresConnectionFactory postgres, string sqliteDbPath)
{
    public async Task<MigrationResult> MigrateAsync(
        IProgress<MigrationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sessionsCopied    = 0;
        var entriesCopied     = 0;
        var credentialsCopied = 0;

        using var sqlite = new SqliteConnection($"Data Source={sqliteDbPath};Mode=ReadOnly");
        await sqlite.OpenAsync(ct).ConfigureAwait(false);

        // ── Sessions ────────────────────────���─────────────────────────────────
        var sessionIds = await ReadSessionIdsAsync(sqlite, ct).ConfigureAwait(false);
        var total = sessionIds.Count;
        progress?.Report(new MigrationProgress("Sessions", 0, total, $"Migrating {total} sessions…"));

        foreach (var sid in sessionIds)
        {
            ct.ThrowIfCancellationRequested();
            if (await CopySessionAsync(sqlite, sid, ct).ConfigureAwait(false))
                sessionsCopied++;
            progress?.Report(new MigrationProgress("Sessions", sessionsCopied, total));
        }

        // ── Session entries ───────────────────────────────────────────────────
        var entryCount = await CountTableAsync(sqlite, "session_entries", ct).ConfigureAwait(false);
        progress?.Report(new MigrationProgress("Entries", 0, entryCount, $"Migrating {entryCount} entries…"));
        entriesCopied = await CopyEntriesAsync(sqlite, entryCount, progress, ct).ConfigureAwait(false);

        // ── Credentials ────────────────────────────��──────────────────────────
        var credCount = await CountTableAsync(sqlite, "credentials", ct).ConfigureAwait(false);
        progress?.Report(new MigrationProgress("Credentials", 0, credCount, $"Migrating {credCount} credentials…"));
        credentialsCopied = await CopyCredentialsAsync(sqlite, credCount, progress, ct).ConfigureAwait(false);

        progress?.Report(new MigrationProgress("Done", credCount, credCount,
            $"Migration complete: {sessionsCopied} sessions, {entriesCopied} entries, {credentialsCopied} credentials."));

        return new MigrationResult(sessionsCopied, entriesCopied, credentialsCopied);
    }

    private static async Task<List<string>> ReadSessionIdsAsync(SqliteConnection sqlite, CancellationToken ct)
    {
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT session_id FROM sessions ORDER BY started_at";
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var ids = new List<string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            ids.Add(reader.GetString(0));
        return ids;
    }

    private static async Task<int> CountTableAsync(SqliteConnection sqlite, string table, CancellationToken ct)
    {
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is long l ? (int)l : 0;
    }

    private async Task<bool> CopySessionAsync(SqliteConnection sqlite, string sessionId, CancellationToken ct)
    {
        using var readCmd = sqlite.CreateCommand();
        readCmd.CommandText = """
            SELECT session_id, user_id, model, started_at, updated_at, title, mcp_servers
            FROM sessions WHERE session_id = $sid
            """;
        readCmd.Parameters.AddWithValue("$sid", sessionId);

        using var reader = await readCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return false;

        var sid        = reader.GetString(0);
        var userId     = await reader.IsDBNullAsync(1, ct).ConfigureAwait(false) ? "" : reader.GetString(1);
        var model      = await reader.IsDBNullAsync(2, ct).ConfigureAwait(false) ? (object)DBNull.Value : reader.GetString(2);
        var startedAt  = reader.GetString(3);
        var updatedAt  = reader.GetString(4);
        var title      = await reader.IsDBNullAsync(5, ct).ConfigureAwait(false) ? (object)DBNull.Value : reader.GetString(5);
        var mcpServers = await reader.IsDBNullAsync(6, ct).ConfigureAwait(false) ? (object)DBNull.Value : reader.GetString(6);

        using var pg = postgres.CreateConnection();
        using var writeCmd = pg.CreateCommand();
        writeCmd.CommandText = """
            INSERT INTO sessions (session_id, user_id, model, started_at, updated_at, title, mcp_servers)
            VALUES ($1,$2,$3,$4,$5,$6,$7)
            ON CONFLICT (session_id) DO NOTHING
            """;
        writeCmd.Parameters.AddWithValue(sid);
        writeCmd.Parameters.AddWithValue(userId);
        writeCmd.Parameters.AddWithValue(model);
        writeCmd.Parameters.AddWithValue(startedAt);
        writeCmd.Parameters.AddWithValue(updatedAt);
        writeCmd.Parameters.AddWithValue(title);
        writeCmd.Parameters.AddWithValue(mcpServers);
        var rows = await writeCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return rows > 0;
    }

    private async Task<int> CopyEntriesAsync(
        SqliteConnection sqlite,
        int total,
        IProgress<MigrationProgress>? progress,
        CancellationToken ct)
    {
        using var readCmd = sqlite.CreateCommand();
        readCmd.CommandText = """
            SELECT session_id, entry_uid, timestamp, role, content, model, provider,
                   input_tokens, output_tokens, tool_name, tool_use_id, is_error
            FROM session_entries ORDER BY entry_id
            """;

        using var reader = await readCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        using var pg = postgres.CreateConnection();

        var copied = 0;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            var model     = await reader.IsDBNullAsync(5, ct).ConfigureAwait(false) ? (object)DBNull.Value : reader.GetString(5);
            var provider  = await reader.IsDBNullAsync(6, ct).ConfigureAwait(false) ? (object)DBNull.Value : reader.GetString(6);
            var toolName  = await reader.IsDBNullAsync(9, ct).ConfigureAwait(false)  ? (object)DBNull.Value : reader.GetString(9);
            var toolUseId = await reader.IsDBNullAsync(10, ct).ConfigureAwait(false) ? (object)DBNull.Value : reader.GetString(10);

            using var writeCmd = pg.CreateCommand();
            writeCmd.CommandText = """
                INSERT INTO session_entries
                    (session_id, entry_uid, timestamp, role, content, model, provider,
                     input_tokens, output_tokens, tool_name, tool_use_id, is_error)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
                ON CONFLICT DO NOTHING
                """;
            writeCmd.Parameters.AddWithValue(reader.GetString(0));
            writeCmd.Parameters.AddWithValue(reader.GetString(1));
            writeCmd.Parameters.AddWithValue(reader.GetString(2));
            writeCmd.Parameters.AddWithValue(reader.GetString(3));
            writeCmd.Parameters.AddWithValue(reader.GetString(4));
            writeCmd.Parameters.AddWithValue(model);
            writeCmd.Parameters.AddWithValue(provider);
            writeCmd.Parameters.AddWithValue(reader.GetInt32(7));
            writeCmd.Parameters.AddWithValue(reader.GetInt32(8));
            writeCmd.Parameters.AddWithValue(toolName);
            writeCmd.Parameters.AddWithValue(toolUseId);
            writeCmd.Parameters.AddWithValue(reader.GetInt32(11));

            var rows = await writeCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (rows > 0) copied++;

            if (copied % 50 == 0)
                progress?.Report(new MigrationProgress("Entries", copied, total));
        }
        return copied;
    }

    private async Task<int> CopyCredentialsAsync(
        SqliteConnection sqlite,
        int total,
        IProgress<MigrationProgress>? progress,
        CancellationToken ct)
    {
        using var readCmd = sqlite.CreateCommand();
        readCmd.CommandText = "SELECT key_hash, user_id, nonce, tag, ciphertext, updated_at FROM credentials";

        using var reader = await readCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        using var pg = postgres.CreateConnection();

        var copied = 0;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            var userId = await reader.IsDBNullAsync(1, ct).ConfigureAwait(false) ? "" : reader.GetString(1);

            using var writeCmd = pg.CreateCommand();
            writeCmd.CommandText = """
                INSERT INTO credentials (key_hash, user_id, nonce, tag, ciphertext, updated_at)
                VALUES ($1,$2,$3,$4,$5,$6)
                ON CONFLICT (key_hash) DO NOTHING
                """;
            writeCmd.Parameters.AddWithValue(reader.GetString(0));
            writeCmd.Parameters.AddWithValue(userId);
            writeCmd.Parameters.AddWithValue((byte[])reader[2]);
            writeCmd.Parameters.AddWithValue((byte[])reader[3]);
            writeCmd.Parameters.AddWithValue((byte[])reader[4]);
            writeCmd.Parameters.AddWithValue(reader.GetString(5));

            var rows = await writeCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (rows > 0) copied++;
        }

        progress?.Report(new MigrationProgress("Credentials", copied, total));
        return copied;
    }
}
