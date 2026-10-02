using System.Globalization;
using Npgsql;
using Sovrant.Runtime.Session;

namespace Sovrant.Storage.Postgres;

/// <summary>
/// Phase 133 — Postgres twin of <c>SqliteSessionFolderStore</c>. Registered with
/// <see cref="PostgresSessionStore"/> so conversation folders live in the same
/// database as the sessions they hold. Same rules, same transaction shape: load
/// the owner's folders, check <see cref="SessionFolderRules"/>, write.
/// </summary>
internal sealed class PostgresSessionFolderStore(IPostgresConnectionFactory factory) : ISessionFolderStore
{
    private const string Now = "to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.MS\"Z\"')";
    private const string UniqueViolation = "23505";

    public async Task<IReadOnlyList<SessionFolder>> ListAsync(string ownerUserId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        return await LoadAsync(conn, null, ownerUserId, ct).ConfigureAwait(false);
    }

    public async Task<SessionFolder> CreateAsync(string ownerUserId, string name, string? parentFolderId, CancellationToken ct = default)
    {
        var normalized = SessionFolderRules.NormalizeName(name);
        using var conn = factory.CreateConnection();
        using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        await LockOwnerAsync(conn, tx, ownerUserId, ct).ConfigureAwait(false);

        var folders = await LoadAsync(conn, tx, ownerUserId, ct).ConfigureAwait(false);
        Throw(SessionFolderRules.CheckCreate(folders, parentFolderId, normalized));

        var folderId = $"fld-{Guid.NewGuid():N}";
        var sortOrder = folders.Where(f => f.ParentFolderId == parentFolderId).Select(f => f.SortOrder + 1).DefaultIfEmpty(0).Max();
        using (var cmd = Command(conn, tx, $"""
            INSERT INTO session_folders (folder_id, owner_user_id, parent_folder_id, name, sort_order, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, {Now}, {Now})
            """))
        {
            cmd.Parameters.AddWithValue(folderId);
            cmd.Parameters.AddWithValue(ownerUserId);
            cmd.Parameters.AddWithValue((object?)parentFolderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue(normalized);
            cmd.Parameters.AddWithValue(sortOrder);
            await ExecuteGuardedAsync(cmd, ct).ConfigureAwait(false);
        }

        var created = await GetAsync(conn, tx, ownerUserId, folderId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return created!;
    }

    public async Task<SessionFolder> RenameAsync(string ownerUserId, string folderId, string name, CancellationToken ct = default)
    {
        var normalized = SessionFolderRules.NormalizeName(name);
        using var conn = factory.CreateConnection();
        using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        await LockOwnerAsync(conn, tx, ownerUserId, ct).ConfigureAwait(false);

        var folders = await LoadAsync(conn, tx, ownerUserId, ct).ConfigureAwait(false);
        var folder = folders.FirstOrDefault(f => f.FolderId == folderId)
            ?? throw new SessionFolderException(SessionFolderError.NotFound);
        if (SessionFolderRules.NameTaken(folders, folder.ParentFolderId, normalized, exceptFolderId: folderId))
            throw new SessionFolderException(SessionFolderError.DuplicateName);

        await SetFolderAsync(conn, tx, folderId, folder.ParentFolderId, normalized, ct).ConfigureAwait(false);
        var renamed = await GetAsync(conn, tx, ownerUserId, folderId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return renamed!;
    }

    public async Task<SessionFolder> MoveAsync(string ownerUserId, string folderId, string? newParentFolderId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        await LockOwnerAsync(conn, tx, ownerUserId, ct).ConfigureAwait(false);

        var folders = await LoadAsync(conn, tx, ownerUserId, ct).ConfigureAwait(false);
        Throw(SessionFolderRules.CheckMove(folders, folderId, newParentFolderId));
        var folder = folders.First(f => f.FolderId == folderId);

        await SetFolderAsync(conn, tx, folderId, newParentFolderId, folder.Name, ct).ConfigureAwait(false);
        var moved = await GetAsync(conn, tx, ownerUserId, folderId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return moved!;
    }

    public async Task<bool> DeleteAsync(string ownerUserId, string folderId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        await LockOwnerAsync(conn, tx, ownerUserId, ct).ConfigureAwait(false);

        var folders = (await LoadAsync(conn, tx, ownerUserId, ct).ConfigureAwait(false)).ToList();
        var folder = folders.FirstOrDefault(f => f.FolderId == folderId);
        if (folder is null)
            return false;

        using (var cmd = Command(conn, tx, "UPDATE sessions SET folder_id = $1 WHERE folder_id = $2 AND user_id = $3"))
        {
            cmd.Parameters.AddWithValue((object?)folder.ParentFolderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue(folderId);
            cmd.Parameters.AddWithValue(ownerUserId);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        folders.Remove(folder);
        foreach (var child in folders.Where(f => f.ParentFolderId == folderId).ToList())
        {
            var newName = SessionFolderRules.UniqueName(folders, folder.ParentFolderId, child.Name, exceptFolderId: child.FolderId);
            await SetFolderAsync(conn, tx, child.FolderId, folder.ParentFolderId, newName, ct).ConfigureAwait(false);
            folders[folders.IndexOf(child)] = child with { ParentFolderId = folder.ParentFolderId, Name = newName };
        }

        using (var cmd = Command(conn, tx, "DELETE FROM session_folders WHERE folder_id = $1 AND owner_user_id = $2"))
        {
            cmd.Parameters.AddWithValue(folderId);
            cmd.Parameters.AddWithValue(ownerUserId);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> MoveSessionAsync(string ownerUserId, string sessionId, string? folderId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        if (folderId is not null && await GetAsync(conn, tx, ownerUserId, folderId, ct).ConfigureAwait(false) is null)
            throw new SessionFolderException(SessionFolderError.NotFound);

        using var cmd = Command(conn, tx, "UPDATE sessions SET folder_id = $1 WHERE session_id = $2 AND user_id = $3");
        cmd.Parameters.AddWithValue((object?)folderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(sessionId);
        cmd.Parameters.AddWithValue(ownerUserId);
        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return rows > 0;
    }

    /// <summary>
    /// Serializes tree writes per owner for the life of the transaction, so two
    /// concurrent moves can't each pass the cycle/depth check and jointly break it.
    /// </summary>
    private static async Task LockOwnerAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string ownerUserId, CancellationToken ct)
    {
        using var cmd = Command(conn, tx, "SELECT pg_advisory_xact_lock(hashtext('session_folders:' || $1))");
        cmd.Parameters.AddWithValue(ownerUserId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task SetFolderAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string folderId, string? parentFolderId, string name, CancellationToken ct)
    {
        using var cmd = Command(conn, tx, $"UPDATE session_folders SET parent_folder_id = $1, name = $2, updated_at = {Now} WHERE folder_id = $3");
        cmd.Parameters.AddWithValue((object?)parentFolderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(name);
        cmd.Parameters.AddWithValue(folderId);
        await ExecuteGuardedAsync(cmd, ct).ConfigureAwait(false);
    }

    private static async Task ExecuteGuardedAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        try
        {
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            throw new SessionFolderException(SessionFolderError.DuplicateName);
        }
    }

    private const string Select =
        "SELECT folder_id, owner_user_id, parent_folder_id, name, sort_order, created_at, updated_at FROM session_folders";

    private static async Task<IReadOnlyList<SessionFolder>> LoadAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx, string ownerUserId, CancellationToken ct)
    {
        using var cmd = Command(conn, tx, $"{Select} WHERE owner_user_id = $1 ORDER BY sort_order, lower(name)");
        cmd.Parameters.AddWithValue(ownerUserId);
        return await ReadAsync(cmd, ct).ConfigureAwait(false);
    }

    private static async Task<SessionFolder?> GetAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string ownerUserId, string folderId, CancellationToken ct)
    {
        using var cmd = Command(conn, tx, $"{Select} WHERE owner_user_id = $1 AND folder_id = $2");
        cmd.Parameters.AddWithValue(ownerUserId);
        cmd.Parameters.AddWithValue(folderId);
        var rows = await ReadAsync(cmd, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    private static async Task<IReadOnlyList<SessionFolder>> ReadAsync(NpgsqlCommand cmd, CancellationToken ct)
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

    private static NpgsqlCommand Command(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return cmd;
    }

    private static void Throw(SessionFolderError? error)
    {
        if (error is { } e)
            throw new SessionFolderException(e);
    }
}
