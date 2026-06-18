using Sovrant.Runtime.Config;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// An MCP server configuration row with its stable database ID and display name.
/// The <see cref="Id"/> is a surrogate key that survives renames; use it for
/// workspace-scoped gating so that renaming a server doesn't break gating rules.
/// </summary>
public sealed record McpServerEntry(string Id, string Name, McpServerConfig Config);

/// <summary>
/// Persistence for MCP server entries (command/args/env + OAuth metadata).
/// The OAuth client secret and access tokens never live here — they are
/// stored encrypted under <see cref="ICredentialStore"/> keys
/// <c>mcp.{name}.client_secret</c>, <c>mcp.{name}.access_token</c>, and
/// <c>mcp.{name}.refresh_token</c>.
/// </summary>
public interface IMcpServerStore
{
    /// <summary>Lists every configured MCP server, keyed by name.</summary>
    Task<IReadOnlyDictionary<string, McpServerConfig>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Lists every configured MCP server as entries with stable IDs and names.</summary>
    Task<IReadOnlyList<McpServerEntry>> GetAllEntriesAsync(CancellationToken ct = default);

    /// <summary>Returns one MCP server config by name, or null if missing.</summary>
    Task<McpServerConfig?> GetAsync(string name, CancellationToken ct = default);

    /// <summary>Returns one MCP server entry (with id) by name, or null if missing.</summary>
    Task<McpServerEntry?> GetEntryAsync(string name, CancellationToken ct = default);

    /// <summary>Inserts or updates the MCP server entry.</summary>
    Task UpsertAsync(string name, McpServerConfig config, CancellationToken ct = default);

    /// <summary>Removes an MCP server entry; no-op if missing.</summary>
    Task DeleteAsync(string name, CancellationToken ct = default);
}
