using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed <see cref="IWorkspaceSettingsStore"/>. Reads/writes the
/// <c>workspace_settings</c> table created in V018.
/// </summary>
internal sealed class SqliteWorkspaceSettingsStore(ISqliteConnectionFactory connectionFactory) : IWorkspaceSettingsStore
{
    public async Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT value FROM workspace_settings
            WHERE key = $key AND workspace_id IN ($ws, '')
            ORDER BY CASE WHEN workspace_id = $ws THEN 0 ELSE 1 END
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$ws", workspaceId);
        cmd.Parameters.AddWithValue("$key", key);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is string s ? s : null;
    }

    public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default) =>
        GetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, key, ct);

    public async Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO workspace_settings (workspace_id, key, value)
            VALUES ($ws, $key, $value)
            ON CONFLICT(workspace_id, key) DO UPDATE SET
                value      = excluded.value,
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            """;
        cmd.Parameters.AddWithValue("$ws", workspaceId);
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM workspace_settings WHERE workspace_id = $ws AND key = $key";
        cmd.Parameters.AddWithValue("$ws", workspaceId);
        cmd.Parameters.AddWithValue("$key", key);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT key, value, workspace_id FROM workspace_settings
            WHERE workspace_id IN ($ws, '')
            """;
        cmd.Parameters.AddWithValue("$ws", workspaceId);

        var globals = new Dictionary<string, string>(StringComparer.Ordinal);
        var locals = new Dictionary<string, string>(StringComparer.Ordinal);

        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            var key = r.GetString(0);
            var value = r.GetString(1);
            var ws = r.GetString(2);
            if (ws == workspaceId)
                locals[key] = value;
            else
                globals[key] = value;
        }

        foreach (var (k, v) in locals)
            globals[k] = v;
        return globals;
    }
}
