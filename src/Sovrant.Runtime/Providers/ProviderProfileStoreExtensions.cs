namespace Sovrant.Runtime.Providers;

/// <summary>
/// Convenience helpers over <see cref="IProviderProfileStore"/> that compose
/// the personal + workspace profile lookups both Web and Desktop perform
/// when populating their model-picker UI.
/// </summary>
public static class ProviderProfileStoreExtensions
{
    /// <summary>
    /// Returns the user's personal profiles followed by any workspace-level
    /// profiles visible to them, deduped by <see cref="ProviderProfile.ProfileId"/>
    /// (personal wins). Workspace rows carry <c>WorkspaceId != null</c> so callers
    /// can distinguish them via <see cref="ProviderProfile.IsAdminManaged"/>.
    /// </summary>
    public static async Task<IReadOnlyList<ProviderProfile>> ListUserAndWorkspaceAsync(
        this IProviderProfileStore store,
        string userId,
        string? workspaceId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var personal = await store.ListAsync(userId, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(workspaceId))
            return personal;

        var workspace = await store.ListByWorkspaceAsync(workspaceId, ct).ConfigureAwait(false);
        if (workspace.Count == 0)
            return personal;

        var seen = new HashSet<string>(personal.Count + workspace.Count, StringComparer.Ordinal);
        var combined = new List<ProviderProfile>(personal.Count + workspace.Count);
        foreach (var p in personal)
        {
            if (seen.Add(p.ProfileId))
                combined.Add(p);
        }
        foreach (var w in workspace)
        {
            if (seen.Add(w.ProfileId))
                combined.Add(w);
        }
        return combined;
    }
}
