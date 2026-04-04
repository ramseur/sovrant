using Sovrant.Lsp.Types;

namespace Sovrant.Lsp;

/// <summary>
/// Manages the lifecycle of a language server process and provides
/// typed access to LSP requests (hover, definition, references, diagnostics, rename).
/// </summary>
public interface ILspClient : IAsyncDisposable
{
    /// <summary>The language identifier this client serves (e.g., "csharp", "python").</summary>
    string Language { get; }

    /// <summary>Whether the language server is running and initialized.</summary>
    bool IsRunning { get; }

    /// <summary>Starts the language server process and sends the LSP <c>initialize</c> handshake.</summary>
    Task StartAsync(string workspaceRoot, CancellationToken ct = default);

    /// <summary>Sends <c>shutdown</c> and <c>exit</c> to the language server, then kills the process.</summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Sends <c>textDocument/hover</c> and returns type/documentation info.</summary>
    Task<HoverResult?> HoverAsync(string filePath, int line, int character, CancellationToken ct = default);

    /// <summary>Sends <c>textDocument/definition</c> and returns the definition location(s).</summary>
    Task<IReadOnlyList<Location>> DefinitionAsync(string filePath, int line, int character, CancellationToken ct = default);

    /// <summary>Sends <c>textDocument/references</c> and returns all reference locations.</summary>
    Task<IReadOnlyList<Location>> ReferencesAsync(string filePath, int line, int character, CancellationToken ct = default);

    /// <summary>Returns the most recently published diagnostics for the given file.</summary>
    IReadOnlyList<Diagnostic> GetDiagnostics(string filePath);

    /// <summary>Sends <c>textDocument/rename</c> and returns the proposed workspace edit.</summary>
    Task<WorkspaceEdit?> RenameAsync(string filePath, int line, int character, string newName, CancellationToken ct = default);

    /// <summary>Notifies the server that a file was opened (sends <c>textDocument/didOpen</c>).</summary>
    Task NotifyDidOpenAsync(string filePath, string text, CancellationToken ct = default);

    /// <summary>Notifies the server that a file was changed (sends <c>textDocument/didChange</c> with full sync).</summary>
    Task NotifyDidChangeAsync(string filePath, string text, CancellationToken ct = default);
}
