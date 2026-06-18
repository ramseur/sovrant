using Sovrant.Runtime.Workspaces;

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
    /// <para>
    /// When <paramref name="wsSettings"/> is supplied and the workspace has an
    /// explicit <c>provider.enabled_profile_ids</c> setting, only profiles whose
    /// IDs appear in that list are returned. A <c>null</c> setting (never configured)
    /// falls through to show all; an empty string means the admin opted in no providers.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyList<ProviderProfile>> ListUserAndWorkspaceAsync(
        this IProviderProfileStore store,
        string userId,
        string? workspaceId,
        IWorkspaceSettingsStore? wsSettings = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var personal = await store.ListAsync(userId, ct).ConfigureAwait(false);

        // Apply workspace-level provider filter when settings are available.
        // Null key (never configured) is treated the same as empty — strict opt-in.
        // Admin must explicitly enable providers per workspace for them to appear.
        if (wsSettings is not null && !string.IsNullOrEmpty(workspaceId))
        {
            var raw = await wsSettings.GetAsync(workspaceId, WorkspaceSettingsKeys.EnabledProviderProfileIds, ct)
                .ConfigureAwait(false);
            var enabledIds = new HashSet<string>(
                (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.Ordinal);
            personal = personal.Where(p => enabledIds.Contains(p.ProfileId)).ToList();
        }

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
