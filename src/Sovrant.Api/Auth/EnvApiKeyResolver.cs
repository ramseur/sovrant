namespace Sovrant.Api.Auth;

/// <summary>
/// Bootstrap-window <see cref="IApiKeyResolver"/> used before the credential store is open.
/// Returns the static fallback value only. <c>Sovrant.Runtime</c> replaces this with
/// <c>CredentialStoreApiKeyResolver</c> once <see cref="AddSovrantRuntime"/> has run.
/// </summary>
public sealed class FallbackApiKeyResolver : IApiKeyResolver
{
    public Task<string?> ResolveAsync(string credentialKey, string? fallback, CancellationToken ct = default)
        => Task.FromResult(string.IsNullOrEmpty(fallback) ? null : fallback);
}

/// <summary>Kept for source compatibility; redirects to <see cref="FallbackApiKeyResolver"/>.</summary>
[Obsolete("Use FallbackApiKeyResolver. EnvApiKeyResolver is removed — API keys live in the credential store only.")]
public sealed class EnvApiKeyResolver : IApiKeyResolver
{
    public Task<string?> ResolveAsync(string credentialKey, string? fallback, CancellationToken ct = default)
        => Task.FromResult(string.IsNullOrEmpty(fallback) ? null : fallback);
}
