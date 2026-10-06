namespace Sovrant.Runtime.Session;

/// <summary>What a sidebar row represents.</summary>
public enum SessionTreeRowKind
{
    Folder,
    Session,
}

/// <summary>
/// Phase 133 — one rendered row of the conversation sidebar. Web and Desktop
/// both render this flat list (indented by <see cref="Depth"/>), so the tree
/// logic lives here once instead of twice.
/// </summary>
/// <param name="Id">The folder id or session id.</param>
/// <param name="FolderId">For a session: the folder it's filed in. For a folder: its parent.</param>
/// <param name="Count">Folders only: conversations in the folder and every subfolder.</param>
/// <param name="Meta">Sessions only: the derived label line ("Agent · x, Workflow · Running"), or the folder path in search results.</param>
public sealed record SessionTreeRow(
    SessionTreeRowKind Kind,
    string Id,
    string Text,
    int Depth,
    string? FolderId,
    int Count = 0,
    bool IsExpanded = false,
    bool HasChildren = false,
    string? Meta = null,
    bool MetaActive = false,
    DateTimeOffset UpdatedAt = default);

/// <summary>Phase 133 — builds sidebar rows from a user's folders and conversations.</summary>
public static class SessionFolderTree
{
    /// <summary>Shown for a conversation that has neither a title nor any message yet.</summary>
    public const string UntitledText = "Untitled conversation";

    /// <summary>
    /// The FOLDERS section: each folder, then — when expanded — its subfolders and
    /// its conversations (newest first). Collapsed folders hide everything below them.
    /// </summary>
    public static IReadOnlyList<SessionTreeRow> FolderRows(
        IReadOnlyList<SessionFolder> folders,
        IReadOnlyList<SessionListItem> sessions,
        IReadOnlySet<string> expandedFolderIds)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(expandedFolderIds);

        var known = folders.Select(f => f.FolderId).ToHashSet(StringComparer.Ordinal);
        var childFolders = folders.ToLookup(f => f.ParentFolderId ?? string.Empty, StringComparer.Ordinal);
        var filed = sessions.Where(s => s.FolderId is not null && known.Contains(s.FolderId))
            .ToLookup(s => s.FolderId!, StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        int Count(string folderId, int guard)
        {
            if (counts.TryGetValue(folderId, out var c))
                return c;
            c = filed[folderId].Count();
            if (guard <= folders.Count)
                c += childFolders[folderId].Sum(child => Count(child.FolderId, guard + 1));
            counts[folderId] = c;
            return c;
        }

        var rows = new List<SessionTreeRow>();
        void Walk(string parentKey, int depth)
        {
            if (depth > SessionFolderRules.MaxDepth + 1)
                return; // corrupt data guard; the stores never allow this
            foreach (var folder in Ordered(childFolders[parentKey]))
            {
                var expanded = expandedFolderIds.Contains(folder.FolderId);
                var hasChildren = childFolders[folder.FolderId].Any() || filed[folder.FolderId].Any();
                rows.Add(new SessionTreeRow(SessionTreeRowKind.Folder, folder.FolderId, folder.Name, depth, folder.ParentFolderId,
                    Count: Count(folder.FolderId, 0), IsExpanded: expanded, HasChildren: hasChildren));
                if (!expanded)
                    continue;
                Walk(folder.FolderId, depth + 1);
                rows.AddRange(filed[folder.FolderId].OrderByDescending(s => s.UpdatedAt).Select(s => SessionRow(s, depth + 1)));
            }
        }

        Walk(string.Empty, 0);
        return rows;
    }

    /// <summary>
    /// The UNFILED section: conversations in no folder (or in a folder that no
    /// longer exists), newest first, capped at <paramref name="limit"/>.
    /// </summary>
    public static IReadOnlyList<SessionTreeRow> UnfiledRows(
        IReadOnlyList<SessionFolder> folders, IReadOnlyList<SessionListItem> sessions, int limit = 20)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(sessions);
        var known = folders.Select(f => f.FolderId).ToHashSet(StringComparer.Ordinal);
        return sessions
            .Where(s => s.FolderId is null || !known.Contains(s.FolderId))
            .OrderByDescending(s => s.UpdatedAt)
            .Take(limit)
            .Select(s => SessionRow(s, 0))
            .ToList();
    }

    /// <summary>Search across every folder: matching conversations, flat, with their folder path as the meta line.</summary>
    public static IReadOnlyList<SessionTreeRow> SearchRows(
        IReadOnlyList<SessionFolder> folders, IReadOnlyList<SessionListItem> sessions, string term, int limit = 50)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(sessions);
        if (string.IsNullOrWhiteSpace(term))
            return [];
        return sessions
            .Where(s => DisplayTitle(s).Contains(term.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.UpdatedAt)
            .Take(limit)
            .Select(s => SessionRow(s, 0) with { Meta = s.FolderId is null ? "Unfiled" : PathOf(folders, s.FolderId) is { Length: > 0 } p ? p : "Unfiled", MetaActive = false })
            .ToList();
    }

    /// <summary>"Client A › Proposals" for a folder id; empty when unknown.</summary>
    public static string PathOf(IReadOnlyList<SessionFolder> folders, string? folderId)
    {
        ArgumentNullException.ThrowIfNull(folders);
        var byId = folders.ToDictionary(f => f.FolderId, StringComparer.Ordinal);
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var id = folderId; id is not null && seen.Add(id) && byId.TryGetValue(id, out var f); id = f.ParentFolderId)
            names.Add(f.Name);
        names.Reverse();
        return string.Join(" › ", names);
    }

    /// <summary>Every folder id from the top level down to <paramref name="folderId"/> — used to expand a path.</summary>
    public static IReadOnlyList<string> AncestorsOf(IReadOnlyList<SessionFolder> folders, string? folderId)
    {
        ArgumentNullException.ThrowIfNull(folders);
        var byId = folders.ToDictionary(f => f.FolderId, StringComparer.Ordinal);
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var id = folderId; id is not null && seen.Add(id) && byId.TryGetValue(id, out var f); id = f.ParentFolderId)
            ids.Add(id);
        ids.Reverse();
        return ids;
    }

    /// <summary>
    /// Rows for the Move dialog's folder picker: every folder, fully expanded,
    /// depth-first. Each row carries why it can't be chosen (or <c>null</c> when it can).
    /// </summary>
    public static IReadOnlyList<(SessionFolder Folder, int Depth, SessionFolderError? Refusal)> PickerRows(
        IReadOnlyList<SessionFolder> folders, string? movingFolderId = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        var children = folders.ToLookup(f => f.ParentFolderId ?? string.Empty, StringComparer.Ordinal);
        var rows = new List<(SessionFolder, int, SessionFolderError?)>();
        void Walk(string parentKey, int depth)
        {
            if (depth > SessionFolderRules.MaxDepth)
                return;
            foreach (var folder in Ordered(children[parentKey]))
            {
                var refusal = movingFolderId is null ? null : SessionFolderRules.CheckMove(folders, movingFolderId, folder.FolderId);
                rows.Add((folder, depth, refusal));
                Walk(folder.FolderId, depth + 1);
            }
        }
        Walk(string.Empty, 0);
        return rows;
    }

    /// <summary>The title to show: the stored title, else <see cref="UntitledText"/>.</summary>
    public static string DisplayTitle(SessionListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return string.IsNullOrWhiteSpace(item.Title) ? UntitledText : item.Title!;
    }

    /// <summary>
    /// Fills in a title for untitled conversations (workflow, webhook, and agent-run
    /// chats never get one) from their first message — loading history only for
    /// those, at most <paramref name="max"/> of them, newest first. Nothing is written back.
    /// </summary>
    public static async Task<IReadOnlyList<SessionListItem>> WithFallbackTitlesAsync(
        ISessionStore store, IReadOnlyList<SessionListItem> items, string? ownerUserId, int max = 40, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(items);
        var untitled = items.Where(i => string.IsNullOrWhiteSpace(i.Title))
            .OrderByDescending(i => i.UpdatedAt).Take(max)
            .Select(i => i.SessionId).ToHashSet(StringComparer.Ordinal);
        if (untitled.Count == 0)
            return items;

        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in untitled)
        {
            var entries = await store.LoadAsync(id, ownerUserId, ct).ConfigureAwait(false);
            var first = entries.FirstOrDefault(e => e.Role == "user" && !string.IsNullOrWhiteSpace(e.Content))
                ?? entries.FirstOrDefault(e => e.Role == "assistant" && !string.IsNullOrWhiteSpace(e.Content));
            if (first is not null)
                titles[id] = Shorten(first.Content);
        }
        return items.Select(i => titles.TryGetValue(i.SessionId, out var t) ? i with { Title = t } : i).ToList();
    }

    private static string Shorten(string text)
    {
        var oneLine = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return oneLine.Length > 60 ? string.Concat(oneLine.AsSpan(0, 57), "...") : oneLine;
    }

    private static SessionTreeRow SessionRow(SessionListItem s, int depth) =>
        new(SessionTreeRowKind.Session, s.SessionId, DisplayTitle(s), depth, s.FolderId,
            Meta: SessionLabels.Line(s.Labels) is { Length: > 0 } line ? line : null,
            MetaActive: s.Labels?.Any(l => l.IsActive) == true,
            UpdatedAt: s.UpdatedAt);

    private static IEnumerable<SessionFolder> Ordered(IEnumerable<SessionFolder> folders) =>
        folders.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
}
