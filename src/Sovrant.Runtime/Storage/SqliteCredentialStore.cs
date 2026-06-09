using System.Security.Cryptography;
using System.Text;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed credential store. Same AES-256-GCM encryption as <see cref="AesGcmCredentialStore"/>
/// but stores encrypted blobs in the <c>credentials</c> table and the master key in the
/// <c>keystore</c> table (V039). Everything lives in the single SQLite database file —
/// no separate on-disk files are required.
/// </summary>
/// <remarks>
/// On first boot after upgrading from a version that used a <c>.keystore</c> file, the
/// legacy file is read once, its key is written to the DB, and the file is deleted so
/// the DB becomes the sole source of truth.
/// </remarks>
internal sealed class SqliteCredentialStore : ICredentialStore, IDisposable
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string KeyScope = "default";

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly string? _legacyKeystorePath;
    private byte[]? _masterKey;
    private readonly SemaphoreSlim _keyInit = new(1, 1);

    public SqliteCredentialStore(ISqliteConnectionFactory connectionFactory, string? legacyKeystorePath = null)
    {
        _connectionFactory = connectionFactory;
        _legacyKeystorePath = legacyKeystorePath;
    }

    public async Task StoreAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        var masterKey = await GetOrCreateMasterKeyAsync(ct).ConfigureAwait(false);
        var plaintext = Encoding.UTF8.GetBytes(value);

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(masterKey, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var keyHash = ComputeKeyHash(key);

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO credentials (key_hash, user_id, nonce, tag, ciphertext, updated_at)
            VALUES ($hash, $uid, $nonce, $tag, $ct, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        cmd.Parameters.AddWithValue("$hash", keyHash);
        cmd.Parameters.AddWithValue("$uid", Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName);
        cmd.Parameters.AddWithValue("$nonce", nonce);
        cmd.Parameters.AddWithValue("$tag", tag);
        cmd.Parameters.AddWithValue("$ct", ciphertext);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<string?> RetrieveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var keyHash = ComputeKeyHash(key);

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT nonce, tag, ciphertext FROM credentials WHERE key_hash = $hash";
        cmd.Parameters.AddWithValue("$hash", keyHash);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        var masterKey = await GetOrCreateMasterKeyAsync(ct).ConfigureAwait(false);

        var nonce = (byte[])reader["nonce"];
        var tag = (byte[])reader["tag"];
        var ciphertext = (byte[])reader["ciphertext"];
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(masterKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (AuthenticationTagMismatchException)
        {
            return null;
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var keyHash = ComputeKeyHash(key);

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM credentials WHERE key_hash = $hash";
        cmd.Parameters.AddWithValue("$hash", keyHash);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public void Dispose() => _keyInit.Dispose();

    // ── Master key ────────────────────────────────────────────────────────────

    private async Task<byte[]> GetOrCreateMasterKeyAsync(CancellationToken ct)
    {
        if (_masterKey is not null)
            return _masterKey;

        await _keyInit.WaitAsync(ct).ConfigureAwait(false);
        try
        {
#pragma warning disable CA1508 // double-check pattern
            _masterKey ??= await LoadOrCreateKeyAsync(ct).ConfigureAwait(false);
#pragma warning restore CA1508
            return _masterKey;
        }
        finally
        {
            _keyInit.Release();
        }
    }

    private async Task<byte[]> LoadOrCreateKeyAsync(CancellationToken ct)
    {
        // 1 — Try reading from the keystore table (normal path after V039).
        var fromDb = await ReadKeyFromDbAsync(ct).ConfigureAwait(false);
        if (fromDb is not null)
            return fromDb;

        // 2 — Legacy file migration: read the old .keystore file, promote to DB, delete the file.
        if (_legacyKeystorePath is not null && File.Exists(_legacyKeystorePath))
        {
            var hex = (await File.ReadAllTextAsync(_legacyKeystorePath, ct).ConfigureAwait(false)).Trim();
            if (hex.Length == KeySize * 2)
            {
                await WriteKeyToDbAsync(hex, ct).ConfigureAwait(false);
                TryDeleteLegacyFile(_legacyKeystorePath);
                return Convert.FromHexString(hex);
            }
        }

        // 3 — First run: generate a new key and persist it to the DB.
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        var newHex = Convert.ToHexString(key);
        await WriteKeyToDbAsync(newHex, ct).ConfigureAwait(false);
        return key;
    }

    private async Task<byte[]?> ReadKeyFromDbAsync(CancellationToken ct)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key_hex FROM keystore WHERE scope = $scope";
        cmd.Parameters.AddWithValue("$scope", KeyScope);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        var hex = reader.GetString(0).Trim();
        return hex.Length == KeySize * 2 ? Convert.FromHexString(hex) : null;
    }

    private async Task WriteKeyToDbAsync(string hex, CancellationToken ct)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO keystore (scope, key_hex)
            VALUES ($scope, $hex)
            """;
        cmd.Parameters.AddWithValue("$scope", KeyScope);
        cmd.Parameters.AddWithValue("$hex", hex);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void TryDeleteLegacyFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { /* best-effort — leave the file if locked or unavailable */ }
        catch (UnauthorizedAccessException) { /* best-effort — leave the file if permissions deny deletion */ }
    }

    private static string ComputeKeyHash(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
