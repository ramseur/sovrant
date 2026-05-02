using Sovrant.Api.Auth;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// <see cref="IApiKeyResolver"/> implementation that consults the encrypted
/// <see cref="ICredentialStore"/> between env-var and static fallback, completing
/// the env &gt; store &gt; fallback chain for non-LLM provider keys (OpenRouter,
/// Brave, FireCrawl, Provider). Registered by <c>AddSovrantRuntime</c> as an
/// override for the bootstrap-time <see cref="EnvApiKeyResolver"/>.
/// </summary>
public sealed class CredentialStoreApiKeyResolver : IApiKeyResolver
{
    private readonly ICredentialStore _store;

    public CredentialStoreApiKeyResolver(ICredentialStore store) => _store = store;

    public Task<string?> ResolveAsync(string credentialKey, string envVar, string? fallback, CancellationToken ct = default)
        => CredentialResolver.ResolveAsync(_store, credentialKey, envVar, fallback, ct);
}
