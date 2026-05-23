using System.Text.Json;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// One-shot migrator that imports existing rows from the legacy <c>mcp_servers</c> SQLite
/// table into the <see cref="CredentialStoreMcpServerStore"/> (encrypted credential store).
/// Idempotent — a sentinel key in the credential store prevents re-running.
/// </summary>
internal sealed partial class McpServerStoreMigrator(
    ISqliteConnectionFactory connectionFactory,
    IMcpServerStore serverStore,
    ICredentialStore credentialStore,
    ILogger<McpServerStoreMigrator> logger)
{
    private const string SentinelKey = "mcp.__v1_migrated__";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task MigrateIfNeededAsync(CancellationToken ct = default)
    {
        if (await credentialStore.RetrieveAsync(SentinelKey, ct).ConfigureAwait(false) is not null)
            return;

        var rows = ReadLegacyRows();
        if (rows.Count > 0)
        {
            LogMigrating(logger, rows.Count);
            foreach (var (name, config) in rows)
            {
                await serverStore.UpsertAsync(name, config, ct).ConfigureAwait(false);
                LogMigratedServer(logger, name);
            }
        }

        await credentialStore.StoreAsync(SentinelKey, "done", ct).ConfigureAwait(false);
        LogMigrationComplete(logger, rows.Count);
    }

    private Dictionary<string, McpServerConfig> ReadLegacyRows()
    {
        var result = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal);

#pragma warning disable CA1031 // table may not exist on fresh installs — treat as empty
        try
        {
            using var conn = connectionFactory.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name, command, args_json, env_json, oauth_config_json, url, headers_json FROM mcp_servers";

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var name = r.GetString(0);
                var command = r.GetString(1);
                var argsJson = r.GetString(2);
                var envJson = r.GetString(3);
                var oauthJson = r.IsDBNull(4) ? null : r.GetString(4);
                var url = r.IsDBNull(5) ? null : r.GetString(5);
                var headersJson = r.IsDBNull(6) ? "{}" : r.GetString(6);

                var args = JsonSerializer.Deserialize<List<string>>(argsJson, JsonOpts) ?? [];
                var env = JsonSerializer.Deserialize<Dictionary<string, string>>(envJson, JsonOpts)
                          ?? new Dictionary<string, string>(StringComparer.Ordinal);
                var oauth = oauthJson is null
                    ? null
                    : JsonSerializer.Deserialize<McpOAuthConfig>(oauthJson, JsonOpts);
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, JsonOpts)
                              ?? new Dictionary<string, string>(StringComparer.Ordinal);

                result[name] = new McpServerConfig
                {
                    Command = command,
                    Args = args,
                    Env = env,
                    OAuthConfig = oauth,
                    Url = string.IsNullOrEmpty(url) ? null : new Uri(url),
                    Headers = headers,
                };
            }
        }
        catch (Exception ex)
        {
            LogLegacyTableUnavailable(logger, ex);
        }
#pragma warning restore CA1031

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrating {Count} MCP server config(s) from SQLite to credential store.")]
    private static partial void LogMigrating(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Migrated MCP server '{Name}'.")]
    private static partial void LogMigratedServer(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP server store migration complete ({Count} entries).")]
    private static partial void LogMigrationComplete(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read legacy mcp_servers table (may not exist on fresh install).")]
    private static partial void LogLegacyTableUnavailable(ILogger logger, Exception ex);
}
