using System.Security.Cryptography;
using System.Text;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// <see cref="ICredentialStore"/> implementation that encrypts credentials with AES-256-GCM
/// and stores them in <c>~/.sovrant/credentials/</c>.
/// </summary>
/// <remarks>
/// A 256-bit master key is generated on first use and persisted as
/// <c>~/.sovrant/credentials/.keystore</c> (user-only permissions on POSIX systems).
/// Each credential is stored as a separate file named after the SHA-256 hash of its key.
/// The on-disk format is: <c>nonce(12 bytes) || tag(16 bytes) || ciphertext</c>.
/// </remarks>
public sealed class AesGcmCredentialStore : ICredentialStore, IDisposable
{
    private const int KeySize = 32;   // 256-bit AES key
    private const int NonceSize = 12; // 96-bit GCM nonce
    private const int TagSize = 16;   // 128-bit GCM authentication tag
    private const int MinBlobLength = NonceSize + TagSize + 1;

    private readonly string _credentialsDir;
    private byte[]? _masterKey;
    private readonly SemaphoreSlim _keyInit = new(1, 1);

    /// <inheritdoc/>
    public void Dispose() => _keyInit.Dispose();

    /// <summary>Initialises the store targeting <c>~/.sovrant/credentials/</c>.</summary>
    public AesGcmCredentialStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "credentials"))
    {
    }

    /// <summary>Initialises the store with a custom directory (primarily for testing).</summary>
    internal AesGcmCredentialStore(string credentialsDir)
    {
        _credentialsDir = credentialsDir;
    }

    /// <inheritdoc/>
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

        // Layout: nonce || tag || ciphertext
        var blob = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(blob.AsSpan(0, NonceSize));
        tag.CopyTo(blob.AsSpan(NonceSize, TagSize));
        ciphertext.CopyTo(blob.AsSpan(NonceSize + TagSize));

        var path = GetCredentialPath(key);
        await File.WriteAllBytesAsync(path, blob, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string?> RetrieveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var path = GetCredentialPath(key);
        if (!File.Exists(path))
            return null;

        var masterKey = await GetOrCreateMasterKeyAsync(ct).ConfigureAwait(false);

        var blob = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        if (blob.Length < MinBlobLength)
            return null;

        var nonce = blob[..NonceSize];
        var tag = blob[NonceSize..(NonceSize + TagSize)];
        var ciphertext = blob[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(masterKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (AuthenticationTagMismatchException)
        {
            // Credential file is corrupt or tampered with — treat as missing.
            return null;
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var path = GetCredentialPath(key);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<byte[]> GetOrCreateMasterKeyAsync(CancellationToken ct)
    {
        if (_masterKey is not null)
            return _masterKey;

        await _keyInit.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Another concurrent caller may have initialised the key while we waited on the semaphore.
            // CA1508 false-positive: the null-coalescing assignment is a deliberate double-check pattern.
#pragma warning disable CA1508
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
        Directory.CreateDirectory(_credentialsDir);
        var keystorePath = Path.Combine(_credentialsDir, ".keystore");

        if (File.Exists(keystorePath))
        {
            var hex = (await File.ReadAllTextAsync(keystorePath, ct).ConfigureAwait(false)).Trim();
            if (hex.Length == KeySize * 2)
                return Convert.FromHexString(hex);
        }

        // First run — generate and persist a new master key.
        var key = new byte[KeySize];
        RandomNumberGenerator.Fill(key);
        var hexKey = Convert.ToHexString(key);
        await File.WriteAllTextAsync(keystorePath, hexKey, ct).ConfigureAwait(false);

        // Restrict permissions on POSIX (no-op on Windows, which uses ACLs on the profile dir).
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(keystorePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        return key;
    }

    private string GetCredentialPath(string key)
    {
        // Hash the logical key so arbitrary strings become safe filenames.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_credentialsDir, $"{hash}.enc");
    }
}
