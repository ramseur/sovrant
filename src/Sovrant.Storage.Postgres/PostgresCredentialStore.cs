using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Storage.Postgres;

/// <summary>
/// Postgres-backed credential store using the same AES-256-GCM encryption
/// as the SQLite implementation. The master key is stored in the DB keystore
/// table (V039); the legacy file path is used only for one-time migration.
/// </summary>
internal sealed class PostgresCredentialStore : ICredentialStore, IDisposable
{
    private const int KeySize   = 32;
    private const int NonceSize = 12;
    private const int TagSize   = 16;
    private const string MasterKeyScope = "master";

    private const string UtcNow =
        "to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.MS\"Z\"')";

    private readonly IPostgresConnectionFactory _factory;
    private readonly string? _keystorePath;
    private byte[]? _masterKey;
    private readonly SemaphoreSlim _keyInit = new(1, 1);

    public PostgresCredentialStore(IPostgresConnectionFactory factory, string? keystorePath = null)
    {
        _factory = factory;
        _keystorePath = keystorePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "credentials", ".keystore");
    }

    public async Task StoreAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        var masterKey  = await GetOrCreateMasterKeyAsync(ct).ConfigureAwait(false);
        var plaintext  = Encoding.UTF8.GetBytes(value);
        var nonce      = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var tag        = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(masterKey, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var keyHash = ComputeKeyHash(key);
        using var conn = _factory.CreateConnection();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO credentials (key_hash, user_id, nonce, tag, ciphertext, updated_at)
            VALUES ($1, $2, $3, $4, $5, {UtcNow})
            ON CONFLICT (key_hash) DO UPDATE
                SET nonce = EXCLUDED.nonce, tag = EXCLUDED.tag,
                    ciphertext = EXCLUDED.ciphertext, updated_at = EXCLUDED.updated_at
            """;
        cmd.Parameters.AddWithValue(keyHash);
        cmd.Parameters.AddWithValue(Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName);
        cmd.Parameters.AddWithValue(nonce);
        cmd.Parameters.AddWithValue(tag);
        cmd.Parameters.AddWithValue(ciphertext);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<string?> RetrieveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var keyHash = ComputeKeyHash(key);
        using var conn = _factory.CreateConnection();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT nonce, tag, ciphertext FROM credentials WHERE key_hash = $1";
        cmd.Parameters.AddWithValue(keyHash);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;

        var masterKey  = await GetOrCreateMasterKeyAsync(ct).ConfigureAwait(false);
        var nonce      = (byte[])reader["nonce"];
        var tag        = (byte[])reader["tag"];
        var ciphertext = (byte[])reader["ciphertext"];
        var plaintext  = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(masterKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (AuthenticationTagMismatchException) { return null; }

        return Encoding.UTF8.GetString(plaintext);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var keyHash = ComputeKeyHash(key);
        using var conn = _factory.CreateConnection();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM credentials WHERE key_hash = $1";
        cmd.Parameters.AddWithValue(keyHash);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public void Dispose() => _keyInit.Dispose();

    private async Task<byte[]> GetOrCreateMasterKeyAsync(CancellationToken ct)
    {
        if (_masterKey is not null) return _masterKey;
        await _keyInit.WaitAsync(ct).ConfigureAwait(false);
        try
        {
#pragma warning disable CA1508
            _masterKey ??= await LoadOrCreateKeyAsync(ct).ConfigureAwait(false);
#pragma warning restore CA1508
            return _masterKey;
        }
        finally { _keyInit.Release(); }
    }

    private async Task<byte[]> LoadOrCreateKeyAsync(CancellationToken ct)
    {
        // Try the DB keystore first (V039+).
        var dbKey = await TryLoadKeyFromDbAsync(ct).ConfigureAwait(false);
        if (dbKey is not null) return dbKey;

        // One-time migration from legacy file if it exists.
        if (_keystorePath is not null && File.Exists(_keystorePath))
        {
            var hex = (await File.ReadAllTextAsync(_keystorePath, ct).ConfigureAwait(false)).Trim();
            if (hex.Length == KeySize * 2)
            {
                var fileKey = Convert.FromHexString(hex);
                await SaveKeyToDbAsync(fileKey, ct).ConfigureAwait(false);
                return fileKey;
            }
        }

        var newKey = new byte[KeySize];
        RandomNumberGenerator.Fill(newKey);
        await SaveKeyToDbAsync(newKey, ct).ConfigureAwait(false);
        return newKey;
    }

    private async Task<byte[]?> TryLoadKeyFromDbAsync(CancellationToken ct)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = "SELECT key_hex FROM keystore WHERE scope = $1";
            cmd.Parameters.AddWithValue(MasterKeyScope);
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (result is string hex && hex.Length == KeySize * 2)
                return Convert.FromHexString(hex);
            return null;
        }
        catch (NpgsqlException) { return null; }
    }

    private async Task SaveKeyToDbAsync(byte[] key, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO keystore (scope, key_hex, created_at)
            VALUES ($1, $2, {UtcNow})
            ON CONFLICT (scope) DO UPDATE SET key_hex = EXCLUDED.key_hex
            """;
        cmd.Parameters.AddWithValue(MasterKeyScope);
        cmd.Parameters.AddWithValue(Convert.ToHexString(key));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static string ComputeKeyHash(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash).ToUpperInvariant();
    }
}
