using Microsoft.Data.Sqlite;
using Sovrant.Runtime.Hooks;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed <see cref="IHookStore"/>. Reads/writes the <c>hooks</c> table
/// created in V017.
/// </summary>
internal sealed class SqliteHookStore(ISqliteConnectionFactory connectionFactory) : IHookStore
{
    public async Task<IReadOnlyList<HookConfig>> GetEnabledAsync(CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hook_id, event, command, matcher_tool, matcher_glob, timeout_ms, enabled
            FROM hooks
            WHERE enabled = 1
            ORDER BY hook_id
            """;
        return await ReadHooksAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HookConfig>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hook_id, event, command, matcher_tool, matcher_glob, timeout_ms, enabled
            FROM hooks
            ORDER BY hook_id
            """;
        return await ReadHooksAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task UpsertAsync(HookConfig hook, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(hook);
        if (string.IsNullOrEmpty(hook.Id))
            throw new ArgumentException("HookConfig.Id must be set before upsert.", nameof(hook));

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO hooks
                (hook_id, event, command, matcher_tool, matcher_glob, timeout_ms, enabled)
            VALUES
                ($id, $event, $command, $tool, $glob, $timeout, $enabled)
            ON CONFLICT(hook_id) DO UPDATE SET
                event        = excluded.event,
                command      = excluded.command,
                matcher_tool = excluded.matcher_tool,
                matcher_glob = excluded.matcher_glob,
                timeout_ms   = excluded.timeout_ms,
                enabled      = excluded.enabled,
                updated_at   = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            """;
        cmd.Parameters.AddWithValue("$id", hook.Id);
        cmd.Parameters.AddWithValue("$event", hook.Event.ToString());
        cmd.Parameters.AddWithValue("$command", hook.Command);
        cmd.Parameters.AddWithValue("$tool", (object?)hook.Matcher?.Tool ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$glob", (object?)hook.Matcher?.Glob ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$timeout", hook.TimeoutMs);
        cmd.Parameters.AddWithValue("$enabled", hook.Enabled ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string hookId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(hookId);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM hooks WHERE hook_id = $id";
        cmd.Parameters.AddWithValue("$id", hookId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<HookConfig>> ReadHooksAsync(SqliteCommand cmd, CancellationToken ct)
    {
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var list = new List<HookConfig>();
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            var tool = await r.IsDBNullAsync(3, ct).ConfigureAwait(false) ? null : r.GetString(3);
            var glob = await r.IsDBNullAsync(4, ct).ConfigureAwait(false) ? null : r.GetString(4);
            HookMatcher? matcher = (tool is null && glob is null) ? null : new HookMatcher(tool, glob);

            list.Add(new HookConfig(
                Event: Enum.Parse<HookEvent>(r.GetString(1), ignoreCase: true),
                Command: r.GetString(2),
                Matcher: matcher,
                TimeoutMs: r.GetInt32(5),
                Id: r.GetString(0),
                Enabled: r.GetInt32(6) == 1));
        }
        return list;
    }
}
