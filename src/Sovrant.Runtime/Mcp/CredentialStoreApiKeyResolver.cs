using Sovrant.Api.Auth;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// <see cref="IApiKeyResolver"/> that reads exclusively from the encrypted
/// <see cref="ICredentialStore"/>. Registered by <c>AddSovrantRuntime</c> to replace
/// the bootstrap-time <see cref="FallbackApiKeyResolver"/> once the store is open.
/// </summary>
public sealed class CredentialStoreApiKeyResolver : IApiKeyResolver
{
    private readonly ICredentialStore _store;

    public CredentialStoreApiKeyResolver(ICredentialStore store) => _store = store;

    public Task<string?> ResolveAsync(string credentialKey, string? fallback, CancellationToken ct = default)
        => CredentialResolver.ResolveAsync(_store, credentialKey, fallback, ct);
}
