namespace Sovrant.Api.Auth;

/// <summary>
/// Resolves a provider API key from the credential store. API keys are never read
/// from environment variables — the encrypted store is the sole source of truth.
/// Lives in <c>Sovrant.Api</c> so consumers (live metadata fetcher, providers) can
/// depend on it without pulling in the Runtime layer.
/// </summary>
public interface IApiKeyResolver
{
    /// <summary>
    /// Returns the credential store entry under <paramref name="credentialKey"/>,
    /// falling back to <paramref name="fallback"/> if not found.
    /// Returns <see langword="null"/> when nothing resolves.
    /// </summary>
    /// <param name="credentialKey">Well-known store key (see <see cref="CredentialKeys"/>).</param>
    /// <param name="fallback">Static fallback for the bootstrap window before the store is open.</param>
    Task<string?> ResolveAsync(string credentialKey, string? fallback, CancellationToken ct = default);
}
