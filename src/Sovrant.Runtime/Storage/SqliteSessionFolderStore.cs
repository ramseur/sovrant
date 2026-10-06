using System.Globalization;
using Microsoft.Data.Sqlite;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 133 — SQLite store for conversation folders (<c>session_folders</c>,
/// <c>sessions.folder_id</c>; V048). Each write loads the owner's folders and
/// checks <see cref="SessionFolderRules"/> inside one write transaction, so two
/// concurrent moves can't jointly create a cycle or exceed the depth limit.
/// A user has tens to hundreds of folders, so loading the whole tree is cheap.
/// </summary>
internal sealed class SqliteSessionFolderStore(ISqliteConnectionFactory connectionFactory) : ISessionFolderStore
{
    private const string Now = "strftime('%Y-%m-%dT%H:%M:%fZ', 'now')";

    public async Task<IReadOnlyList<SessionFolder>> ListAsync(string ownerUserId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await LoadAsync(connection, null, ownerUserId, ct).ConfigureAwait(false);
    }

    public async Task<SessionFolder> CreateAsync(string ownerUserId, string name, string? parentFolderId, CancellationToken ct = default)
    {
        var normalized = SessionFolderRules.NormalizeName(name);
        using var connection = connectionFactory.CreateConnection();
        using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        var folders = await LoadAsync(connection, tx, ownerUserId, ct).ConfigureAwait(false);
        Throw(SessionFolderRules.CheckCreate(folders, parentFolderId, normalized));

        var folderId = $"fld-{Guid.NewGuid():N}";
        var sortOrder = folders.Where(f => f.ParentFolderId == parentFolderId).Select(f => f.SortOrder + 1).DefaultIfEmpty(0).Max();
        using (var cmd = Command(connection, tx, $"""
            INSERT INTO session_folders (folder_id, owner_user_id, parent_folder_id, name, sort_order, created_at, updated_at)
            VALUES ($id, $owner, $parent, $name, $sort, {Now}, {Now})
            """))
        {
            cmd.Parameters.AddWithValue("$id", folderId);
            cmd.Parameters.AddWithValue("$owner", ownerUserId);
            cmd.Parameters.AddWithValue("$parent", (object?)parentFolderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$name", normalized);
            cmd.Parameters.AddWithValue("$sort", sortOrder);
            try
            {
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // sibling-name index caught a race
            {
                throw new SessionFolderException(SessionFolderError.DuplicateName);
            }
        }

        var created = await GetAsync(connection, tx, ownerUserId, folderId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return created!;
    }

    public async Task<SessionFolder> RenameAsync(string ownerUserId, string folderId, string name, CancellationToken ct = default)
    {
        var normalized = SessionFolderRules.NormalizeName(name);
        using var connection = connectionFactory.CreateConnection();
        using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        var folders = await LoadAsync(connection, tx, ownerUserId, ct).ConfigureAwait(false);
        var folder = folders.FirstOrDefault(f => f.FolderId == folderId)
            ?? throw new SessionFolderException(SessionFolderError.NotFound);
        if (SessionFolderRules.NameTaken(folders, folder.ParentFolderId, normalized, exceptFolderId: folderId))
            throw new SessionFolderException(SessionFolderError.DuplicateName);

        await SetFolderAsync(connection, tx, folderId, folder.ParentFolderId, normalized, ct).ConfigureAwait(false);
        var renamed = await GetAsync(connection, tx, ownerUserId, folderId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return renamed!;
    }

    public async Task<SessionFolder> MoveAsync(string ownerUserId, string folderId, string? newParentFolderId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        var folders = await LoadAsync(connection, tx, ownerUserId, ct).ConfigureAwait(false);
        Throw(SessionFolderRules.CheckMove(folders, folderId, newParentFolderId));
        var folder = folders.First(f => f.FolderId == folderId);

        await SetFolderAsync(connection, tx, folderId, newParentFolderId, folder.Name, ct).ConfigureAwait(false);
        var moved = await GetAsync(connection, tx, ownerUserId, folderId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return moved!;
    }

    public async Task<bool> DeleteAsync(string ownerUserId, string folderId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        var folders = (await LoadAsync(connection, tx, ownerUserId, ct).ConfigureAwait(false)).ToList();
        var folder = folders.FirstOrDefault(f => f.FolderId == folderId);
        if (folder is null)
            return false;

        // Conversations move up to the deleted folder's parent.
        using (var cmd = Command(connection, tx, "UPDATE sessions SET folder_id = $parent WHERE folder_id = $id AND user_id = $owner"))
        {
            cmd.Parameters.AddWithValue("$parent", (object?)folder.ParentFolderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$id", folderId);
            cmd.Parameters.AddWithValue("$owner", ownerUserId);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // Subfolders move up too, renamed "(2)", "(3)", … if their name is taken at the new level.
        // Moving up one level can only make a subtree shallower, so the depth limit still holds.
        folders.Remove(folder);
        foreach (var child in folders.Where(f => f.ParentFolderId == folderId).ToList())
        {
            var newName = SessionFolderRules.UniqueName(folders, folder.ParentFolderId, child.Name, exceptFolderId: child.FolderId);
            await SetFolderAsync(connection, tx, child.FolderId, folder.ParentFolderId, newName, ct).ConfigureAwait(false);
            folders[folders.IndexOf(child)] = child with { ParentFolderId = folder.ParentFolderId, Name = newName };
        }

        using (var cmd = Command(connection, tx, "DELETE FROM session_folders WHERE folder_id = $id AND owner_user_id = $owner"))
        {
            cmd.Parameters.AddWithValue("$id", folderId);
            cmd.Parameters.AddWithValue("$owner", ownerUserId);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> MoveSessionAsync(string ownerUserId, string sessionId, string? folderId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        if (folderId is not null && await GetAsync(connection, tx, ownerUserId, folderId, ct).ConfigureAwait(false) is null)
            throw new SessionFolderException(SessionFolderError.NotFound);

        // Owner predicate inside the UPDATE: another user's conversation is a silent no-op.
        using var cmd = Command(connection, tx, "UPDATE sessions SET folder_id = $folder WHERE session_id = $sid AND user_id = $owner");
        cmd.Parameters.AddWithValue("$folder", (object?)folderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$owner", ownerUserId);
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return rows > 0;
    }

    private static async Task SetFolderAsync(
        SqliteConnection connection, SqliteTransaction tx, string folderId, string? parentFolderId, string name, CancellationToken ct)
    {
        using var cmd = Command(connection, tx, $"UPDATE session_folders SET parent_folder_id = $parent, name = $name, updated_at = {Now} WHERE folder_id = $id");
        cmd.Parameters.AddWithValue("$parent", (object?)parentFolderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$id", folderId);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT: the sibling-name index caught a race
        {
            throw new SessionFolderException(SessionFolderError.DuplicateName);
        }
    }

    private static async Task<IReadOnlyList<SessionFolder>> LoadAsync(
        SqliteConnection connection, SqliteTransaction? tx, string ownerUserId, CancellationToken ct)
    {
        using var cmd = Command(connection, tx, $"{Select} WHERE owner_user_id = $owner ORDER BY sort_order, name COLLATE NOCASE");
        cmd.Parameters.AddWithValue("$owner", ownerUserId);
        return await ReadAsync(cmd, ct).ConfigureAwait(false);
    }

    private static async Task<SessionFolder?> GetAsync(
        SqliteConnection connection, SqliteTransaction tx, string ownerUserId, string folderId, CancellationToken ct)
    {
        using var cmd = Command(connection, tx, $"{Select} WHERE owner_user_id = $owner AND folder_id = $id");
        cmd.Parameters.AddWithValue("$owner", ownerUserId);
        cmd.Parameters.AddWithValue("$id", folderId);
        var rows = await ReadAsync(cmd, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    private const string Select =
        "SELECT folder_id, owner_user_id, parent_folder_id, name, sort_order, created_at, updated_at FROM session_folders";

    private static async Task<IReadOnlyList<SessionFolder>> ReadAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var result = new List<SessionFolder>();
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new SessionFolder(
                FolderId: r.GetString(0),
                OwnerUserId: r.GetString(1),
                ParentFolderId: await r.IsDBNullAsync(2, ct).ConfigureAwait(false) ? null : r.GetString(2),
                Name: r.GetString(3),
                SortOrder: r.GetInt32(4),
                CreatedAt: DateTimeOffset.Parse(r.GetString(5), CultureInfo.InvariantCulture),
                UpdatedAt: DateTimeOffset.Parse(r.GetString(6), CultureInfo.InvariantCulture)));
        }
        return result;
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? tx, string sql)
    {
        var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
#pragma warning disable CA2100 // every caller passes SQL built from constants; values are bound as parameters
        cmd.CommandText = sql;
#pragma warning restore CA2100
        return cmd;
    }

    private static void Throw(SessionFolderError? error)
    {
        if (error is { } e)
            throw new SessionFolderException(e);
    }
}
