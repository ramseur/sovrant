namespace Sovrant.Api.Auth;

/// <summary>
/// Default <see cref="IApiKeyResolver"/> for the bootstrap window before the credential
/// store is available. Reads the environment variable, then falls back to the supplied
/// snapshot value. <c>Sovrant.Runtime</c> overrides this with a store-aware implementation
/// once <c>AddSovrantRuntime</c> has run.
/// </summary>
public sealed class EnvApiKeyResolver : IApiKeyResolver
{
    public Task<string?> ResolveAsync(string credentialKey, string envVar, string? fallback, CancellationToken ct = default)
    {
        var env = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(env))
            return Task.FromResult<string?>(env);

        return Task.FromResult(string.IsNullOrEmpty(fallback) ? null : fallback);
    }
}
