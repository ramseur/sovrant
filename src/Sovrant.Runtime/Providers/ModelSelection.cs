using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Providers;

/// <summary>A conversation's provider profile and model.</summary>
public sealed record ModelPick(string ProfileId, string Model);

/// <summary>
/// Phase 145 Part B — which admin-configured model a member's conversation uses on a shared server.
/// Members have no providers or keys of their own; they pick any model allowed on the workspace
/// they're in, and that pick is theirs alone (it's applied to their conversation, never written to
/// the global config). Resolution order: the member's saved pick if that profile is allowed here →
/// the workspace's default profile → the first allowed profile → null (the install default).
/// </summary>
public static class ModelSelection
{
    public static async Task<ModelPick?> ResolveAsync(
        string userId,
        string? workspaceId,
        IUserPreferenceStore prefs,
        IProviderProfileStore profiles,
        IWorkspaceSettingsStore? wsSettings,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefs);
        ArgumentNullException.ThrowIfNull(profiles);
        if (string.IsNullOrEmpty(userId))
            return null;

        var allowed = await profiles.ListUserAndWorkspaceAsync(userId, workspaceId, wsSettings, ct).ConfigureAwait(false);
        if (allowed.Count == 0)
            return null;

        var savedProfile = await prefs.GetAsync(userId, UserPreferenceKeys.ActiveProviderProfileId, ct).ConfigureAwait(false);
        var savedModel = await prefs.GetAsync(userId, UserPreferenceKeys.Model, ct).ConfigureAwait(false);
        if (allowed.FirstOrDefault(p => p.ProfileId == savedProfile) is { } mine)
            return Pick(mine, savedModel);

        // The member's pick isn't allowed here (other workspace, or removed by an admin): the
        // workspace default, then the first allowed profile.
        string? workspaceDefault = null;
        if (wsSettings is not null && !string.IsNullOrEmpty(workspaceId))
            workspaceDefault = await wsSettings.GetAsync(workspaceId, WorkspaceSettingsKeys.ActiveProviderProfileId, ct).ConfigureAwait(false);
        var fallback = allowed.FirstOrDefault(p => p.ProfileId == workspaceDefault) ?? allowed[0];
        return Pick(fallback, model: null);
    }

    private static ModelPick? Pick(ProviderProfile profile, string? model)
    {
        var chosen = !string.IsNullOrWhiteSpace(model) ? model : profile.DefaultModel;
        return string.IsNullOrWhiteSpace(chosen) ? null : new ModelPick(profile.ProfileId, chosen!);
    }
}
