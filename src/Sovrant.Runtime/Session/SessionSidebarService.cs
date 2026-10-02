using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Preferences;

namespace Sovrant.Runtime.Session;

/// <summary>Everything a conversation sidebar needs for one render.</summary>
public sealed record SessionSidebarSnapshot(
    IReadOnlyList<SessionFolder> Folders,
    IReadOnlyList<SessionListItem> Sessions);

/// <summary>
/// Phase 133 — loads and edits the conversation sidebar for Web and Desktop, in
/// embedded and remote mode alike. Built straight from whichever stores the host
/// registered (<see cref="From"/>), so it works whether or not the full runtime is
/// in-process. Which folders are expanded is a per-user preference stored in the
/// DB (<see cref="ExpandedFoldersKey"/>), not on disk.
/// </summary>
public sealed class SessionSidebarService
{
    /// <summary><c>user_preferences</c> key holding a JSON array of expanded folder ids.</summary>
    public const string ExpandedFoldersKey = "sidebar.expanded_folders";

    private readonly ISessionStore _sessions;
    private readonly ISessionFolderStore _folders;
    private readonly ISessionLinkResolver _links;
    private readonly IUserPreferenceStore? _prefs;

    public SessionSidebarService(
        ISessionStore sessions, ISessionFolderStore folders, ISessionLinkResolver links, IUserPreferenceStore? prefs = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(links);
        _sessions = sessions;
        _folders = folders;
        _links = links;
        _prefs = prefs;
    }

    /// <summary>Builds the service from a host's container; the preference store is optional.</summary>
    public static SessionSidebarService From(IServiceProvider services) => new(
        services.GetRequiredService<ISessionStore>(),
        services.GetRequiredService<ISessionFolderStore>(),
        services.GetRequiredService<ISessionLinkResolver>(),
        services.GetService<IUserPreferenceStore>());

    /// <summary>The user's folders and visible conversations, labelled and titled.</summary>
    public async Task<SessionSidebarSnapshot> LoadAsync(string ownerUserId, CancellationToken ct = default)
    {
        var folders = await _folders.ListAsync(ownerUserId, ct).ConfigureAwait(false);
        var listed = await _sessions.ListWithTitlesAsync(ownerUserId, ct).ConfigureAwait(false);
        var visible = listed.Where(s => !SessionLabels.IsSystemSession(s.SessionId)).ToList();
        var labelled = visible.Any(s => s.Labels is null)
            ? await _links.WithLabelsAsync(visible, ct).ConfigureAwait(false)
            : visible;
        var titled = await SessionFolderTree.WithFallbackTitlesAsync(_sessions, labelled, ownerUserId, ct: ct).ConfigureAwait(false);
        return new SessionSidebarSnapshot(folders, titled);
    }

    public Task<SessionFolder> CreateFolderAsync(string ownerUserId, string name, string? parentFolderId, CancellationToken ct = default) =>
        _folders.CreateAsync(ownerUserId, name, parentFolderId, ct);

    public Task<SessionFolder> RenameFolderAsync(string ownerUserId, string folderId, string name, CancellationToken ct = default) =>
        _folders.RenameAsync(ownerUserId, folderId, name, ct);

    public Task<SessionFolder> MoveFolderAsync(string ownerUserId, string folderId, string? newParentFolderId, CancellationToken ct = default) =>
        _folders.MoveAsync(ownerUserId, folderId, newParentFolderId, ct);

    public Task<bool> DeleteFolderAsync(string ownerUserId, string folderId, CancellationToken ct = default) =>
        _folders.DeleteAsync(ownerUserId, folderId, ct);

    public Task<bool> MoveSessionAsync(string ownerUserId, string sessionId, string? folderId, CancellationToken ct = default) =>
        _folders.MoveSessionAsync(ownerUserId, sessionId, folderId, ct);

    /// <summary>The user's expanded folders; empty when nothing is saved or no preference store exists.</summary>
    public async Task<HashSet<string>> LoadExpandedAsync(string ownerUserId, CancellationToken ct = default)
    {
        if (_prefs is null)
            return new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var json = await _prefs.GetAsync(ownerUserId, ExpandedFoldersKey, ct).ConfigureAwait(false);
            var ids = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<string[]>(json);
            return new HashSet<string>(ids ?? [], StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    /// <summary>Saves which folders are expanded. No-op without a preference store.</summary>
    public Task SaveExpandedAsync(string ownerUserId, IEnumerable<string> expandedFolderIds, CancellationToken ct = default) =>
        _prefs is null
            ? Task.CompletedTask
            : _prefs.SetAsync(ownerUserId, ExpandedFoldersKey,
                JsonSerializer.Serialize(expandedFolderIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()), ct);
}
