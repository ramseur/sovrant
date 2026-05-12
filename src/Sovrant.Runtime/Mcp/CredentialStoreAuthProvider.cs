using Sovrant.Api.Auth;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// <see cref="IAuthProvider"/> that resolves the primary LLM API key from the encrypted
/// credential store on every request. API keys are never read from environment variables.
/// </summary>
/// <remarks>
/// The store value is read once and cached after the first successful read. Call
/// <see cref="Invalidate"/> after writing a new key via the settings UI or CLI.
/// </remarks>
public sealed class CredentialStoreAuthProvider : IAuthProvider
{
    private readonly ICredentialStore _store;
    private readonly string _credentialKey;
    private readonly string _fallback;
    private string? _cachedStoreValue;

    public CredentialStoreAuthProvider(ICredentialStore store, string credentialKey, string fallback)
    {
        _store = store;
        _credentialKey = credentialKey;
        _fallback = fallback ?? string.Empty;
    }

    public async ValueTask<string> GetAuthHeaderAsync(CancellationToken ct)
    {
        if (_cachedStoreValue is null)
        {
            try
            {
                _cachedStoreValue = await _store.RetrieveAsync(_credentialKey, ct).ConfigureAwait(false)
                    ?? string.Empty;
            }
            catch (Exception ex) when (ex is InvalidOperationException
                or System.Data.Common.DbException
                or IOException
                or UnauthorizedAccessException)
            {
                // Store unavailable (e.g. DB not yet migrated) — fall through to the static value.
                _cachedStoreValue = string.Empty;
            }
        }

        return !string.IsNullOrEmpty(_cachedStoreValue) ? _cachedStoreValue : _fallback;
    }

    /// <summary>
    /// Drops the cached store value so the next request re-reads from the credential store.
    /// Call this after writing a new value via <c>sovrant auth set</c> or the settings UI.
    /// </summary>
    public void Invalidate() => _cachedStoreValue = null;
}
