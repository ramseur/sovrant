namespace Sovrant.Runtime.Mcp;

/// <summary>
/// Persistence for LSP server entries (per-language launch metadata).
/// LSP processes carry no per-user secrets — env vars set here apply to
/// every session that reaches the language server.
/// </summary>
public interface ILspServerStore
{
    /// <summary>Lists every configured LSP server, keyed by language identifier.</summary>
    Task<IReadOnlyDictionary<string, LspServerEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns one LSP server entry by language id, or null if missing.</summary>
    Task<LspServerEntry?> GetAsync(string language, CancellationToken ct = default);

    /// <summary>Inserts or updates the LSP server entry.</summary>
    Task UpsertAsync(string language, LspServerEntry entry, CancellationToken ct = default);

    /// <summary>Removes an LSP server entry; no-op if missing.</summary>
    Task DeleteAsync(string language, CancellationToken ct = default);
}

/// <summary>Launch metadata for one LSP server.</summary>
public sealed record LspServerEntry(
    string Command,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Env)
{
    /// <summary>Empty entry — convenient default.</summary>
    public static LspServerEntry Empty { get; } = new(
        string.Empty,
        [],
        new Dictionary<string, string>(StringComparer.Ordinal));
}
