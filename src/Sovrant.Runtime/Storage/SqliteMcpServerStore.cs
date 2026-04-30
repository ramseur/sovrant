using System.Text.Json;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed <see cref="IMcpServerStore"/>. Reads/writes the
/// <c>mcp_servers</c> table created in V019.
/// </summary>
/// <remarks>
/// The <c>oauth_config_json</c> column never contains the OAuth client secret;
/// secrets live encrypted in <see cref="Sovrant.Runtime.Security.ICredentialStore"/>
/// under key <c>mcp.{name}.client_secret</c>.
/// </remarks>
internal sealed class SqliteMcpServerStore(ISqliteConnectionFactory connectionFactory) : IMcpServerStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IReadOnlyDictionary<string, McpServerConfig>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, command, args_json, env_json, oauth_config_json FROM mcp_servers";

        var result = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal);
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            var name = r.GetString(0);
            result[name] = ReadEntry(r);
        }
        return result;
    }

    public async Task<McpServerConfig?> GetAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, command, args_json, env_json, oauth_config_json FROM mcp_servers WHERE name = $name";
        cmd.Parameters.AddWithValue("$name", name);

        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await r.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return ReadEntry(r);
    }

    public async Task UpsertAsync(string name, McpServerConfig config, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(config);

        var argsJson = JsonSerializer.Serialize(config.Args, JsonOpts);
        var envJson = JsonSerializer.Serialize(config.Env, JsonOpts);
        var oauthJson = config.OAuthConfig is null ? null : JsonSerializer.Serialize(config.OAuthConfig, JsonOpts);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mcp_servers (name, command, args_json, env_json, oauth_config_json)
            VALUES ($name, $command, $args, $env, $oauth)
            ON CONFLICT(name) DO UPDATE SET
                command           = excluded.command,
                args_json         = excluded.args_json,
                env_json          = excluded.env_json,
                oauth_config_json = excluded.oauth_config_json,
                updated_at        = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$command", config.Command ?? string.Empty);
        cmd.Parameters.AddWithValue("$args", argsJson);
        cmd.Parameters.AddWithValue("$env", envJson);
        cmd.Parameters.AddWithValue("$oauth", (object?)oauthJson ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM mcp_servers WHERE name = $name";
        cmd.Parameters.AddWithValue("$name", name);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static McpServerConfig ReadEntry(System.Data.Common.DbDataReader r)
    {
        var command = r.GetString(1);
        var argsJson = r.GetString(2);
        var envJson = r.GetString(3);
        var oauthJson = r.IsDBNull(4) ? null : r.GetString(4);

        var args = JsonSerializer.Deserialize<List<string>>(argsJson, JsonOpts) ?? [];
        var env = JsonSerializer.Deserialize<Dictionary<string, string>>(envJson, JsonOpts)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var oauth = oauthJson is null ? null : JsonSerializer.Deserialize<McpOAuthConfig>(oauthJson, JsonOpts);

        return new McpServerConfig
        {
            Command = command,
            Args = args,
            Env = env,
            OAuthConfig = oauth,
        };
    }
}
