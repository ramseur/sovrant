namespace Sovrant.Runtime.Mcp;

/// <summary>
/// One-shot lookup helper for secondary provider keys (Brave, Firecrawl, OpenRouter, etc.)
/// Reads exclusively from the encrypted credential store. API keys are never read from
/// environment variables.
/// </summary>
public static class CredentialResolver
{
    /// <summary>
    /// Returns the first non-empty value from: <paramref name="store"/>'s entry under
    /// <paramref name="credentialKey"/>, then <paramref name="fallback"/>.
    /// Returns <see langword="null"/> if nothing resolves.
    /// </summary>
    public static async Task<string?> ResolveAsync(
        ICredentialStore? store,
        string credentialKey,
        string? fallback,
        CancellationToken ct = default)
    {
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
