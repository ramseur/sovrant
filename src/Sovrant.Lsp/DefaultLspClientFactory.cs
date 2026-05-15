using Microsoft.Extensions.Logging;

namespace Sovrant.Lsp;

/// <summary>Default <see cref="ILspClientFactory"/> that creates <see cref="LspClient"/> instances.</summary>
public sealed class DefaultLspClientFactory : ILspClientFactory
{
    /// <inheritdoc/>
    public ILspClient Create(LspServerConfig config, ILogger logger) => new LspClient(config, logger);
}
