using System.Text.Json;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed <see cref="ILspServerStore"/>. Reads/writes the
/// <c>lsp_servers</c> table created in V019.
/// </summary>
internal sealed class SqliteLspServerStore(ISqliteConnectionFactory connectionFactory) : ILspServerStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<IReadOnlyDictionary<string, LspServerEntry>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT language, command, args_json, env_json FROM lsp_servers";

        var result = new Dictionary<string, LspServerEntry>(StringComparer.OrdinalIgnoreCase);
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            var language = r.GetString(0);
            result[language] = ReadEntry(r);
        }
        return result;
    }

    public async Task<LspServerEntry?> GetAsync(string language, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT language, command, args_json, env_json FROM lsp_servers WHERE language = $lang";
        cmd.Parameters.AddWithValue("$lang", language);

        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await r.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return ReadEntry(r);
    }

    public async Task UpsertAsync(string language, LspServerEntry entry, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);
        ArgumentNullException.ThrowIfNull(entry);

        var argsJson = JsonSerializer.Serialize(entry.Args, JsonOpts);
        var envJson = JsonSerializer.Serialize(entry.Env, JsonOpts);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO lsp_servers (language, command, args_json, env_json)
            VALUES ($lang, $command, $args, $env)
            ON CONFLICT(language) DO UPDATE SET
                command    = excluded.command,
                args_json  = excluded.args_json,
                env_json   = excluded.env_json,
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            """;
        cmd.Parameters.AddWithValue("$lang", language);
        cmd.Parameters.AddWithValue("$command", entry.Command ?? string.Empty);
        cmd.Parameters.AddWithValue("$args", argsJson);
        cmd.Parameters.AddWithValue("$env", envJson);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string language, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM lsp_servers WHERE language = $lang";
        cmd.Parameters.AddWithValue("$lang", language);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static LspServerEntry ReadEntry(System.Data.Common.DbDataReader r)
    {
        var command = r.GetString(1);
        var argsJson = r.GetString(2);
        var envJson = r.GetString(3);

        var args = JsonSerializer.Deserialize<List<string>>(argsJson, JsonOpts) ?? [];
        var env = JsonSerializer.Deserialize<Dictionary<string, string>>(envJson, JsonOpts)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        return new LspServerEntry(command, args, env);
    }
}
