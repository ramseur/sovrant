using Sovrant.Api.Auth;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Server.Auth;

/// <summary>
/// An <see cref="IAuthProvider"/> that reads the LLM API key from the encrypted
/// <see cref="ICredentialStore"/> under the well-known key
/// <see cref="LlmApiKeyCredentialKey"/>. The decrypted value is cached in memory
/// after first load so the hot path doesn't hit disk per LLM call.
/// </summary>
internal sealed class MutableApiKeyAuthProvider : IAuthProvider
{
    /// <summary>Storage key used by the server to persist the LLM API key.</summary>
    public const string LlmApiKeyCredentialKey = "llm:api_key";

    private readonly ICredentialStore _store;
    private volatile string? _cached;

    public MutableApiKeyAuthProvider(ICredentialStore store) => _store = store;

    /// <inheritdoc/>
    public async ValueTask<string> GetAuthHeaderAsync(CancellationToken ct)
    {
        if (_cached is not null)
            return _cached;

        var value = await _store.RetrieveAsync(LlmApiKeyCredentialKey, ct).ConfigureAwait(false) ?? string.Empty;
        _cached = value;
        return value;
    }
}
