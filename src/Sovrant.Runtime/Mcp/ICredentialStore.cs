namespace Sovrant.Runtime.Mcp;

/// <summary>
/// Encrypted, persistent storage for OAuth access tokens and other sensitive credentials.
/// Credentials are scoped by an opaque string key and never appear in logs or session history.
/// </summary>
public interface ICredentialStore
{
    /// <summary>Encrypts and persists a credential value under the given key.</summary>
    /// <param name="key">An opaque storage key — must be non-empty.</param>
    /// <param name="value">The plaintext value to encrypt and store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StoreAsync(string key, string value, CancellationToken ct = default);

    /// <summary>
    /// Retrieves and decrypts a previously stored credential.
    /// Returns <see langword="null"/> if no entry exists for the key.
    /// </summary>
    /// <param name="key">The storage key used when the credential was stored.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> RetrieveAsync(string key, CancellationToken ct = default);

    /// <summary>Deletes a stored credential. No-op if the key does not exist.</summary>
    /// <param name="key">The storage key of the credential to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string key, CancellationToken ct = default);
}
