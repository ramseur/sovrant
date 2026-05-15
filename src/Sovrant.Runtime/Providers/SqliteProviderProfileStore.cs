using System.Globalization;
using Microsoft.Data.Sqlite;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Providers;

/// <summary>
/// SQLite-backed <see cref="IProviderProfileStore"/> reading/writing the
/// <c>provider_profiles</c> table introduced in V021.
/// </summary>
internal sealed class SqliteProviderProfileStore(ISqliteConnectionFactory connectionFactory) : IProviderProfileStore
{
    public async Task<IReadOnlyList<ProviderProfile>> ListAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectColumns} WHERE user_id = $uid ORDER BY created_at ASC";
        cmd.Parameters.AddWithValue("$uid", userId);

        var result = new List<ProviderProfile>();
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            result.Add(Read(r));
        return result;
    }

    public async Task<ProviderProfile?> GetAsync(string profileId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileId);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectColumns} WHERE profile_id = $id LIMIT 1";
        cmd.Parameters.AddWithValue("$id", profileId);

        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false) ? Read(r) : null;
    }

    public async Task<IReadOnlyList<ProviderProfile>> ListByWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workspaceId);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectColumns} WHERE workspace_id = $wid ORDER BY created_at ASC";
        cmd.Parameters.AddWithValue("$wid", workspaceId);

        var result = new List<ProviderProfile>();
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            result.Add(Read(r));
        return result;
    }

    public async Task CreateAsync(ProviderProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO provider_profiles
                (profile_id, user_id, name, provider_kind, base_url,
                 default_model, max_tokens, credential_id, workspace_id, created_at, updated_at)
            VALUES
                ($id, $uid, $name, $kind, $url,
                 $model, $tokens, $cred, $wid, $created, $updated)
            """;
        BindCommonParams(cmd, profile);
        cmd.Parameters.AddWithValue("$created", profile.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$updated", profile.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAsync(ProviderProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE provider_profiles SET
                user_id       = $uid,
                name          = $name,
                provider_kind = $kind,
                base_url      = $url,
                default_model = $model,
                max_tokens    = $tokens,
                credential_id = $cred,
                workspace_id  = $wid,
                updated_at    = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE profile_id = $id
            """;
        BindCommonParams(cmd, profile);
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(string profileId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileId);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM provider_profiles WHERE profile_id = $id";
        cmd.Parameters.AddWithValue("$id", profileId);
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return rows > 0;
    }

    private const string SelectColumns = """
        SELECT profile_id, user_id, name, provider_kind, base_url,
               default_model, max_tokens, credential_id, created_at, updated_at, workspace_id
        FROM provider_profiles
        """;

    private static void BindCommonParams(SqliteCommand cmd, ProviderProfile p)
    {
        cmd.Parameters.AddWithValue("$id", p.ProfileId);
        cmd.Parameters.AddWithValue("$uid", p.UserId);
        cmd.Parameters.AddWithValue("$name", p.Name);
        cmd.Parameters.AddWithValue("$kind", p.ProviderKind);
        cmd.Parameters.AddWithValue("$url", p.BaseUrl);
        cmd.Parameters.AddWithValue("$model", (object?)p.DefaultModel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tokens", p.MaxTokens.HasValue ? p.MaxTokens.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$cred", p.CredentialId);
        cmd.Parameters.AddWithValue("$wid", (object?)p.WorkspaceId ?? DBNull.Value);
    }

    private static ProviderProfile Read(SqliteDataReader r) => new(
        ProfileId:    r.GetString(0),
        UserId:       r.GetString(1),
        Name:         r.GetString(2),
        ProviderKind: r.GetString(3),
        BaseUrl:      r.GetString(4),
        DefaultModel: r.IsDBNull(5) ? null : r.GetString(5),
        MaxTokens:    r.IsDBNull(6) ? null : r.GetInt32(6),
        CredentialId: r.GetString(7),
        CreatedAt:    DateTimeOffset.Parse(r.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        UpdatedAt:    DateTimeOffset.Parse(r.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        WorkspaceId:  r.IsDBNull(10) ? null : r.GetString(10));
}
