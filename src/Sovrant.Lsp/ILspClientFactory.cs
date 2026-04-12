using Microsoft.Extensions.Logging;

namespace Sovrant.Lsp;

/// <summary>
/// Creates <see cref="ILspClient"/> instances for a given server configuration.
/// Replace in DI to supply custom LSP client implementations (e.g. for testing or remote servers).
/// </summary>
public interface ILspClientFactory
{
    /// <summary>Creates an <see cref="ILspClient"/> for the given language server configuration.</summary>
    ILspClient Create(LspServerConfig config, ILogger logger);
}
