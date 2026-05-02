namespace Sovrant.Runtime.Mcp;

/// <summary>
/// One-shot lookup helper for secondary provider keys (Brave, Firecrawl, OpenRouter, etc.)
/// Mirrors the env &gt; store &gt; static-fallback precedence chain used by
/// <see cref="CredentialStoreAuthProvider"/> but without caching — each call re-reads,
/// which is fine for the rare/manual code paths these keys feed.
/// </summary>
public static class CredentialResolver
{
    /// <summary>
    /// Returns the first non-empty value from: <paramref name="envVar"/> environment
    /// variable, then <paramref name="store"/>'s entry under <paramref name="credentialKey"/>,
    /// then <paramref name="fallback"/>. Returns <see langword="null"/> if nothing resolves.
    /// </summary>
    public static async Task<string?> ResolveAsync(
        ICredentialStore? store,
        string credentialKey,
        string envVar,
        string? fallback,
        CancellationToken ct = default)
    {
        var env = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(env))
            return env;

        if (store is not null)
        {
            try
            {
                var stored = await store.RetrieveAsync(credentialKey, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(stored))
                    return stored;
            }
            catch (Exception ex) when (ex is InvalidOperationException
                or System.Data.Common.DbException
                or IOException
                or UnauthorizedAccessException)
            {
                // Store unavailable (e.g. DB not yet migrated) — fall through to the static value.
            }
        }

        return string.IsNullOrEmpty(fallback) ? null : fallback;
    }
}
