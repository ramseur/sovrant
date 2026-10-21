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
        var own = await store.ListAsync(userId, ct).ConfigureAwait(false);
        var result = new List<ProviderProfile>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (wsSettings is not null && !string.IsNullOrEmpty(workspaceId))
        {
            // Which configured profiles this workspace allows. Strict opt-in: a null or empty list
            // allows none. Phase 145 Part B: personal workspaces use the admin's default model set
            // when one is configured (else their own list, as before).
            string? raw = null;
            if (WorkspaceIdentity.IsPersonal(workspaceId))
                raw = await wsSettings.GetGlobalAsync(WorkspaceSettingsKeys.PersonalDefaultProfileIds, ct).ConfigureAwait(false);
            raw ??= await wsSettings.GetAsync(workspaceId, WorkspaceSettingsKeys.EnabledProviderProfileIds, ct).ConfigureAwait(false);
            var enabledIds = (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var p in own.Where(p => enabledIds.Contains(p.ProfileId, StringComparer.Ordinal)))
                if (seen.Add(p.ProfileId)) result.Add(p);

            // Phase 145 Part B: the admin-configured profiles allowed here. They're owned by the admin
            // who created them, so the member's own list never contained them; members use them (the
            // key never leaves the server) but have no providers or keys of their own.
            foreach (var id in enabledIds)
            {
                if (seen.Contains(id)) continue;
                if (await store.GetAsync(id, ct).ConfigureAwait(false) is { } shared && seen.Add(shared.ProfileId))
                    result.Add(shared);
            }
        }
        else
        {
            foreach (var p in own)
                if (seen.Add(p.ProfileId)) result.Add(p);
        }

        if (string.IsNullOrEmpty(workspaceId))
            return result;

        // Profiles created for this workspace are always allowed in it.
        foreach (var w in await store.ListByWorkspaceAsync(workspaceId, ct).ConfigureAwait(false))
            if (seen.Add(w.ProfileId)) result.Add(w);
        return result;
    }
}
