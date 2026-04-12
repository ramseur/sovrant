using Microsoft.Extensions.Logging;

namespace Sovrant.Lsp;

/// <summary>
/// Manages the lifecycle of multiple <see cref="LspClient"/> instances,
/// one per configured language server. Maps file extensions to languages.
/// </summary>
public sealed class LspClientManager : ILspClientManager
{
    private readonly Dictionary<string, ILspClient> _clients;
    private readonly ILogger _logger;

    /// <summary>Extension-to-language mapping for common file types.</summary>
    private static readonly Dictionary<string, string> s_extensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".csx"] = "csharp",
        [".py"] = "python",
        [".pyi"] = "python",
        [".ts"] = "typescript",
        [".tsx"] = "typescript",
        [".js"] = "javascript",
        [".jsx"] = "javascript",
        [".go"] = "go",
        [".rs"] = "rust",
        [".java"] = "java",
        [".c"] = "c",
        [".cpp"] = "cpp",
        [".h"] = "c",
        [".hpp"] = "cpp",
        [".lua"] = "lua",
        [".rb"] = "ruby",
        [".swift"] = "swift",
        [".kt"] = "kotlin",
        [".zig"] = "zig",
    };

    public LspClientManager(IEnumerable<LspServerConfig> configs, ILoggerFactory loggerFactory, ILspClientFactory? clientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(configs);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _logger = loggerFactory.CreateLogger<LspClientManager>();
        _clients = new Dictionary<string, ILspClient>(StringComparer.OrdinalIgnoreCase);

        var factory = clientFactory ?? new DefaultLspClientFactory();
        foreach (var config in configs)
        {
            if (string.IsNullOrWhiteSpace(config.Language) || string.IsNullOrWhiteSpace(config.Command))
                continue;

            var logger = loggerFactory.CreateLogger($"Sovrant.Lsp.{config.Language}");
            _clients[config.Language] = factory.Create(config, logger);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Languages => [.. _clients.Keys];

    /// <inheritdoc/>
    public ILspClient? GetClient(string language) =>
        _clients.TryGetValue(language, out var client) ? client : null;

    /// <inheritdoc/>
    public ILspClient? GetClientForFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return null;

        if (!s_extensionMap.TryGetValue(ext, out var language)) return null;
        return GetClient(language);
    }

    /// <inheritdoc/>
    public async Task StartAllAsync(string workspaceRoot, CancellationToken ct = default)
    {
        foreach (var (language, client) in _clients)
        {
            try
            {
                await client.StartAsync(workspaceRoot, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start LSP server for {Language}", language);
            }
        }
    }

    /// <inheritdoc/>
    public async Task StopAllAsync(CancellationToken ct = default)
    {
        foreach (var (language, client) in _clients)
        {
            try
            {
                await client.StopAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop LSP server for {Language}", language);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAllAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
