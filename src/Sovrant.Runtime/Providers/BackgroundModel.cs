using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Providers;

/// <summary>
/// Phase 145 — work that runs outside a chat (workflow runs, scheduled jobs, webhooks, swarms started
/// through the API) uses its owner's model pick, from the models allowed in its workspace — the same
/// choice <see cref="ModelSelection"/> makes for their chats — instead of the install default.
/// </summary>
public static class BackgroundModel
{
    /// <summary>
    /// A context to push for <paramref name="ownerUserId"/>'s background work (carrying their model and
    /// provider), or null when there's no owner or nothing they're allowed to use. Push it in the caller
    /// (<c>using var _ = SessionContext.Push(context)</c>): a push inside an async helper is lost when it returns.
    /// </summary>
    public static async Task<SessionConfig?> ContextForAsync(
        string? ownerUserId,
        string? workspaceId,
        string? sessionId,
        IUserPreferenceStore? prefs,
        IProviderProfileStore? profiles,
        IWorkspaceSettingsStore? wsSettings,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ownerUserId) || prefs is null || profiles is null)
            return null;
        var workspace = string.IsNullOrEmpty(workspaceId) ? WorkspaceIdentity.DefaultPersonalFor(ownerUserId) : workspaceId;
        var pick = await ModelSelection.ResolveAsync(ownerUserId, workspace, prefs, profiles, wsSettings, ct).ConfigureAwait(false);
        if (pick is null)
            return null;
        return new SessionConfig
        {
            OwnerUserId = ownerUserId,
            SessionId = sessionId,
            ProviderProfileId = pick.ProfileId,
            Model = pick.Model,
        };
    }
}
