namespace Sovrant.Lsp;

/// <summary>Configuration for a single language server process.</summary>
public sealed class LspServerConfig
{
    /// <summary>The language identifier (e.g., "csharp", "python", "typescript").</summary>
    public string Language { get; init; } = string.Empty;

    /// <summary>The command to launch the language server (e.g., "omnisharp", "pyright-langserver").</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Command-line arguments for the language server.</summary>
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>Additional environment variables for the language server process.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
