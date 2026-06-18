using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Mcp;

/// <summary>Extension helpers for <see cref="IMcpServerStore"/>.</summary>
public static class McpServerStoreExtensions
{
    /// <summary>
    /// Returns the MCP server entries that are enabled for the given workspace.
    /// Strict opt-in: when the workspace setting is null or empty, an empty list is returned.
    /// When <paramref name="wsSettings"/> or <paramref name="workspaceId"/> is absent, all entries
    /// are returned unfiltered (personal context — no workspace gate applies).
    /// </summary>
    public static async Task<IReadOnlyList<McpServerEntry>> GetEnabledEntriesAsync(
        this IMcpServerStore store,
        IWorkspaceSettingsStore? wsSettings,
        string? workspaceId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var allEntries = await store.GetAllEntriesAsync(ct).ConfigureAwait(false);

        if (wsSettings is null || string.IsNullOrEmpty(workspaceId))
            return allEntries;

        var raw = await wsSettings.GetAsync(workspaceId, WorkspaceSettingsKeys.EnabledMcpServerIds, ct)
            .ConfigureAwait(false);
        var enabledIds = new HashSet<string>(
            (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);

        return allEntries.Where(e => enabledIds.Contains(e.Id)).ToList();
    }
}
