using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Preferences;

/// <summary>
/// SQLite-backed <see cref="IUserPreferenceStore"/> reading/writing the
/// <c>user_preferences</c> table introduced in V020.
/// </summary>
internal sealed class SqliteUserPreferenceStore(ISqliteConnectionFactory connectionFactory) : IUserPreferenceStore
{
    public async Task<string?> GetAsync(string userId, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT value FROM user_preferences
            WHERE user_id = $uid AND key = $key
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$key", key);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is string s ? s : null;
    }

    public async Task SetAsync(string userId, string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO user_preferences (user_id, key, value)
            VALUES ($uid, $key, $value)
            ON CONFLICT(user_id, key) DO UPDATE SET
                value      = excluded.value,
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            """;
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string userId, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM user_preferences WHERE user_id = $uid AND key = $key";
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$key", key);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM user_preferences WHERE user_id = $uid";
        cmd.Parameters.AddWithValue("$uid", userId);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            result[r.GetString(0)] = r.GetString(1);
        return result;
    }
}
