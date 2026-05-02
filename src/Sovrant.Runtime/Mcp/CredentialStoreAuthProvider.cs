using Sovrant.Api.Auth;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// <see cref="IAuthProvider"/> that resolves the primary LLM API key on every request
/// using the layered precedence chain: environment variable &gt; encrypted credential store
/// &gt; static fallback (the env-time snapshot captured by <see cref="Config.CredentialConfig"/>).
/// </summary>
/// <remarks>
/// <para>
/// The env-var lookup happens on every call so operators can rotate keys without
/// restarting. The store value is read once on startup and refreshed lazily when the
/// env var is unset — this keeps Settings UI hot-swap fast for the Web/Desktop
/// surfaces (which override <see cref="IAuthProvider"/> with their own mutable
/// implementations) while still working in the CLI / Server cases that rely on this
/// provider directly.
/// </para>
/// <para>
/// The fallback value lets the bootstrap path keep working before migrations have
/// run (the credential store is unusable until SQLite is open).
/// </para>
/// </remarks>
public sealed class CredentialStoreAuthProvider : IAuthProvider
{
    private static readonly string[] s_envVars = ["LLM_API_KEY", "OPENAI_API_KEY", "PROVIDER_API_KEY"];

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
        foreach (var name in s_envVars)
        {
            var env = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(env))
                return env;
        }

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
    /// Drops the cached store value so the next request re-reads from the credential
    /// store. Call this after writing a new value via <c>sovrant auth set</c>.
    /// </summary>
    public void Invalidate() => _cachedStoreValue = null;
}
