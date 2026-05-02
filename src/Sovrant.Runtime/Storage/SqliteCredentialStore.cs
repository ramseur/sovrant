using System.Security.Cryptography;
using System.Text;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed credential store. Same AES-256-GCM encryption as <see cref="AesGcmCredentialStore"/>
/// but stores encrypted blobs in the <c>credentials</c> table instead of individual files.
/// The master key file (<c>.keystore</c>) is the one piece that still lives on disk —
/// its path is sourced from <see cref="Config.BootstrapConfig.KeystorePath"/> via DI.
/// </summary>
internal sealed class SqliteCredentialStore : ICredentialStore, IDisposable
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly string _keystorePath;
    private byte[]? _masterKey;
    private readonly SemaphoreSlim _keyInit = new(1, 1);

    public SqliteCredentialStore(ISqliteConnectionFactory connectionFactory, string? keystorePath = null)
    {
        _connectionFactory = connectionFactory;
        _keystorePath = keystorePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "credentials", ".keystore");
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
        var dir = Path.GetDirectoryName(_keystorePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(_keystorePath))
        {
            var hex = (await File.ReadAllTextAsync(_keystorePath, ct).ConfigureAwait(false)).Trim();
            if (hex.Length == KeySize * 2)
                return Convert.FromHexString(hex);
        }

        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        await File.WriteAllTextAsync(_keystorePath, Convert.ToHexString(key), ct).ConfigureAwait(false);

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_keystorePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        return key;
    }

    private static string ComputeKeyHash(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
