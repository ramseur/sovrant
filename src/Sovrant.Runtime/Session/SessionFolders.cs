using Sovrant.Api.Errors;

namespace Sovrant.Runtime.Session;

/// <summary>
/// Phase 133 — one folder in a user's conversation-folder tree. Folders are
/// per user, span every workspace, and hold conversations (sessions) only.
/// <see cref="ParentFolderId"/> is <c>null</c> for a top-level folder.
/// </summary>
public sealed record SessionFolder(
    string FolderId,
    string OwnerUserId,
    string? ParentFolderId,
    string Name,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Why a folder operation was refused.</summary>
public enum SessionFolderError
{
    /// <summary>The folder (or target parent) doesn't exist or belongs to someone else.</summary>
    NotFound,

    /// <summary>The name is empty, too long, or contains control characters.</summary>
    InvalidName,

    /// <summary>Another folder with the same name (ignoring case) already exists at that level.</summary>
    DuplicateName,

    /// <summary>The operation would put a folder deeper than <see cref="SessionFolderRules.MaxDepth"/>.</summary>
    TooDeep,

    /// <summary>A folder can't be moved into itself or one of its own subfolders.</summary>
    Cycle,
}

/// <summary>Thrown by <see cref="ISessionFolderStore"/> when a tree rule refuses an operation.</summary>
public sealed class SessionFolderException : SovrantException
{
    public SessionFolderError Error { get; }

    public SessionFolderException() : this(SessionFolderError.NotFound) { }
    public SessionFolderException(string message) : base(message) { Error = SessionFolderError.NotFound; }
    public SessionFolderException(string message, Exception? inner) : base(message, inner) { Error = SessionFolderError.NotFound; }

    public SessionFolderException(SessionFolderError error)
        : base(SessionFolderRules.Describe(error))
    {
        Error = error;
    }
}

/// <summary>
/// Phase 133 — persistence for conversation folders. Always registered alongside
/// <see cref="ISessionStore"/> and backed by the same database, so
/// <c>sessions.folder_id</c> never references a table in another database.
/// Every method is scoped to <c>ownerUserId</c>: another user's folder or
/// conversation behaves exactly like one that doesn't exist.
/// </summary>
public interface ISessionFolderStore
{
    /// <summary>Returns every folder the user owns (the whole tree, unordered).</summary>
    Task<IReadOnlyList<SessionFolder>> ListAsync(string ownerUserId, CancellationToken ct = default);

    /// <summary>Creates a folder under <paramref name="parentFolderId"/> (<c>null</c> = top level).</summary>
    /// <exception cref="SessionFolderException">NotFound, InvalidName, DuplicateName, or TooDeep.</exception>
    Task<SessionFolder> CreateAsync(string ownerUserId, string name, string? parentFolderId, CancellationToken ct = default);

    /// <summary>Renames a folder.</summary>
    /// <exception cref="SessionFolderException">NotFound, InvalidName, or DuplicateName.</exception>
    Task<SessionFolder> RenameAsync(string ownerUserId, string folderId, string name, CancellationToken ct = default);

    /// <summary>Moves a folder (and everything inside it) under a new parent (<c>null</c> = top level).</summary>
    /// <exception cref="SessionFolderException">NotFound, DuplicateName, TooDeep, or Cycle.</exception>
    Task<SessionFolder> MoveAsync(string ownerUserId, string folderId, string? newParentFolderId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a folder. Its conversations and subfolders move up to the deleted
    /// folder's parent in the same transaction — no conversation is ever deleted.
    /// A moved subfolder whose name clashes with a folder already at that level
    /// gets a " (2)", " (3)", … suffix. Returns <c>false</c> if the folder wasn't found.
    /// </summary>
    Task<bool> DeleteAsync(string ownerUserId, string folderId, CancellationToken ct = default);

    /// <summary>
    /// Files a conversation into <paramref name="folderId"/> (<c>null</c> = unfiled).
    /// Returns <c>false</c> if the conversation isn't the user's.
    /// </summary>
    /// <exception cref="SessionFolderException">NotFound when the target folder isn't the user's.</exception>
    Task<bool> MoveSessionAsync(string ownerUserId, string sessionId, string? folderId, CancellationToken ct = default);
}

/// <summary>
/// Phase 133 — the folder-tree rules, as pure functions over a user's folder
/// list. The stores enforce them on every write; the Web and Desktop sidebars
/// use the same checks to refuse an invalid drag-and-drop before sending it.
/// </summary>
public static class SessionFolderRules
{
    /// <summary>Folders nest at most this many levels deep (a top-level folder is level 1).</summary>
    public const int MaxDepth = 5;

    /// <summary>Longest allowed folder name, in characters.</summary>
    public const int MaxNameLength = 100;

    /// <summary>Trims <paramref name="name"/> and validates it; throws InvalidName when unusable.</summary>
    public static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > MaxNameLength || trimmed.Any(char.IsControl))
            throw new SessionFolderException(SessionFolderError.InvalidName);
        return trimmed;
    }

    /// <summary>Level of <paramref name="folderId"/> in the tree (top level = 1); <c>0</c> for <c>null</c> (the root).</summary>
    public static int DepthOf(IReadOnlyList<SessionFolder> folders, string? folderId)
    {
        ArgumentNullException.ThrowIfNull(folders);
        var byId = Index(folders);
        var depth = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var id = folderId; id is not null && byId.TryGetValue(id, out var f) && seen.Add(id); id = f.ParentFolderId)
            depth++;
        return depth;
    }

    /// <summary>Levels in the subtree rooted at <paramref name="folderId"/>, counting the folder itself (a leaf = 1).</summary>
    public static int SubtreeHeight(IReadOnlyList<SessionFolder> folders, string folderId)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(folderId);
        var children = folders.ToLookup(f => f.ParentFolderId ?? string.Empty, StringComparer.Ordinal);
        int Height(string id, int guard) =>
            guard > folders.Count ? 1 : 1 + children[id].Select(c => Height(c.FolderId, guard + 1)).DefaultIfEmpty(0).Max();
        return Height(folderId, 0);
    }

    /// <summary><c>true</c> when <paramref name="candidateId"/> is <paramref name="folderId"/> or sits anywhere below it.</summary>
    public static bool IsSelfOrDescendant(IReadOnlyList<SessionFolder> folders, string folderId, string? candidateId)
    {
        ArgumentNullException.ThrowIfNull(folders);
        var byId = Index(folders);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var id = candidateId; id is not null && seen.Add(id); id = byId.TryGetValue(id, out var f) ? f.ParentFolderId : null)
        {
            if (string.Equals(id, folderId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary><c>true</c> when a folder other than <paramref name="exceptFolderId"/> under <paramref name="parentFolderId"/> already uses <paramref name="name"/>.</summary>
    public static bool NameTaken(IReadOnlyList<SessionFolder> folders, string? parentFolderId, string name, string? exceptFolderId = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        return folders.Any(f =>
            string.Equals(f.ParentFolderId, parentFolderId, StringComparison.Ordinal)
            && !string.Equals(f.FolderId, exceptFolderId, StringComparison.Ordinal)
            && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Checks creating a folder named <paramref name="name"/> under <paramref name="parentFolderId"/>; <c>null</c> when allowed.</summary>
    public static SessionFolderError? CheckCreate(IReadOnlyList<SessionFolder> folders, string? parentFolderId, string name)
    {
        ArgumentNullException.ThrowIfNull(folders);
        if (parentFolderId is not null && !Index(folders).ContainsKey(parentFolderId))
            return SessionFolderError.NotFound;
        if (DepthOf(folders, parentFolderId) + 1 > MaxDepth)
            return SessionFolderError.TooDeep;
        if (NameTaken(folders, parentFolderId, name))
            return SessionFolderError.DuplicateName;
        return null;
    }

    /// <summary>Checks moving <paramref name="folderId"/> under <paramref name="newParentFolderId"/>; <c>null</c> when allowed.</summary>
    public static SessionFolderError? CheckMove(IReadOnlyList<SessionFolder> folders, string folderId, string? newParentFolderId)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(folderId);
        var byId = Index(folders);
        if (!byId.TryGetValue(folderId, out var folder))
            return SessionFolderError.NotFound;
        if (newParentFolderId is not null && !byId.ContainsKey(newParentFolderId))
            return SessionFolderError.NotFound;
        if (IsSelfOrDescendant(folders, folderId, newParentFolderId))
            return SessionFolderError.Cycle;
        if (DepthOf(folders, newParentFolderId) + SubtreeHeight(folders, folderId) > MaxDepth)
            return SessionFolderError.TooDeep;
        if (NameTaken(folders, newParentFolderId, folder.Name, exceptFolderId: folderId))
            return SessionFolderError.DuplicateName;
        return null;
    }

    /// <summary>
    /// Returns <paramref name="name"/>, or the first of "name (2)", "name (3)", … that isn't
    /// taken under <paramref name="parentFolderId"/>. Used when a folder delete moves subfolders up.
    /// </summary>
    public static string UniqueName(IReadOnlyList<SessionFolder> folders, string? parentFolderId, string name, string? exceptFolderId = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(name);
        if (!NameTaken(folders, parentFolderId, name, exceptFolderId))
            return name;
        for (var n = 2; ; n++)
        {
            var suffix = $" ({n})";
            var candidate = (name.Length + suffix.Length > MaxNameLength ? name[..(MaxNameLength - suffix.Length)] : name) + suffix;
            if (!NameTaken(folders, parentFolderId, candidate, exceptFolderId))
                return candidate;
        }
    }

    /// <summary>The user-facing sentence for a refusal — shared by the API error body and the drag-and-drop hint.</summary>
    public static string Describe(SessionFolderError error) => error switch
    {
        SessionFolderError.NotFound => "Folder not found.",
        SessionFolderError.InvalidName => $"Folder names must be 1–{MaxNameLength} characters with no control characters.",
        SessionFolderError.DuplicateName => "A folder with that name already exists here.",
        SessionFolderError.TooDeep => $"Folders can only nest {MaxDepth} levels deep.",
        SessionFolderError.Cycle => "Can't move a folder into its own subfolder.",
        _ => "That folder change isn't allowed.",
    };

    private static Dictionary<string, SessionFolder> Index(IReadOnlyList<SessionFolder> folders) =>
        folders.ToDictionary(f => f.FolderId, StringComparer.Ordinal);
}
