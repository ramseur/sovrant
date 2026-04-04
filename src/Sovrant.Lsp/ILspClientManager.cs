namespace Sovrant.Lsp;

/// <summary>
/// Manages the lifecycle of multiple language server clients.
/// Used by tools to get the appropriate LSP client for a given file or language.
/// </summary>
public interface ILspClientManager : IAsyncDisposable
{
    /// <summary>
    /// Returns the LSP client for the given language, or <see langword="null"/>
    /// if no language server is configured for that language.
    /// </summary>
    ILspClient? GetClient(string language);

    /// <summary>
    /// Returns the LSP client that best matches the given file path based on extension,
    /// or <see langword="null"/> if no matching language server is configured.
    /// </summary>
    ILspClient? GetClientForFile(string filePath);

    /// <summary>Starts all configured language servers for the given workspace root.</summary>
    Task StartAllAsync(string workspaceRoot, CancellationToken ct = default);

    /// <summary>Stops all running language servers.</summary>
    Task StopAllAsync(CancellationToken ct = default);

    /// <summary>Returns all configured language identifiers.</summary>
    IReadOnlyList<string> Languages { get; }
}
