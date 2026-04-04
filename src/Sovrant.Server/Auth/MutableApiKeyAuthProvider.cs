using Sovrant.Api.Auth;
using Sovrant.Server.ServerConfig;

namespace Sovrant.Server.Auth;

/// <summary>
/// An <see cref="IAuthProvider"/> that reads the LLM API key from <see cref="MutableServerConfig"/>,
/// allowing the key to be updated at runtime without restarting the server.
/// </summary>
internal sealed class MutableApiKeyAuthProvider : IAuthProvider
{
    private readonly MutableServerConfig _config;

    public MutableApiKeyAuthProvider(MutableServerConfig config) => _config = config;

    /// <inheritdoc/>
    public ValueTask<string> GetAuthHeaderAsync(CancellationToken ct) =>
        ValueTask.FromResult(_config.LlmApiKey);
}
