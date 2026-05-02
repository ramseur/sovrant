namespace Sovrant.Api.Auth;

/// <summary>
/// Resolves a provider API key with the env &gt; credential-store &gt; static-fallback
/// precedence chain established by Bucket-C of the config audit. Lives in
/// <c>Sovrant.Api</c> so consumers (live metadata fetcher, providers) can take a
/// dependency on it without pulling in the Runtime layer where the encrypted
/// store implementation lives.
/// </summary>
public interface IApiKeyResolver
{
    /// <summary>
    /// Returns the first non-empty value from the named environment variable, then
    /// the credential store entry under <paramref name="credentialKey"/>, then the
    /// supplied <paramref name="fallback"/>. Returns <see langword="null"/> when nothing resolves.
    /// </summary>
    /// <param name="credentialKey">Well-known store key (see <see cref="CredentialKeys"/>).</param>
    /// <param name="envVar">Primary environment variable name to consult before the store.</param>
    /// <param name="fallback">Static fallback (typically the env-time snapshot from <c>CredentialConfig</c>).</param>
    Task<string?> ResolveAsync(string credentialKey, string envVar, string? fallback, CancellationToken ct = default);
}
