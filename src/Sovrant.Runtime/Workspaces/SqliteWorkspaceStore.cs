using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Workspaces;

/// <summary>SQLite-backed implementation of <see cref="IWorkspaceService"/>.</summary>
[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Database stores enum values as lowercase by convention.")]
internal sealed class SqliteWorkspaceStore(ISqliteConnectionFactory connectionFactory) : IWorkspaceService
{
    // ── Workspace CRUD ─────────────────────────────────────────────────────

    public async Task<Workspace> CreatePersonalWorkspaceAsync(string userId, CancellationToken ct = default)
    {
        var workspace = new Workspace
        {
            WorkspaceId = WorkspaceIdentity.DefaultPersonalFor(userId),
            Type = WorkspaceType.Personal,
            Name = "Personal",
            Slug = $"personal-{userId}",
            OwnerId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO workspaces (workspace_id, type, name, slug, owner_id, created_at, updated_at)
            VALUES ($wid, $type, $name, $slug, $owner, $created, $updated)
            """;
        cmd.Parameters.AddWithValue("$wid", workspace.WorkspaceId);
        cmd.Parameters.AddWithValue("$type", workspace.Type.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$name", workspace.Name);
        cmd.Parameters.AddWithValue("$slug", workspace.Slug);
        cmd.Parameters.AddWithValue("$owner", workspace.OwnerId);
        cmd.Parameters.AddWithValue("$created", Ts(workspace.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", Ts(workspace.UpdatedAt));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Add owner as member.
        using var memberCmd = connection.CreateCommand();
        memberCmd.CommandText = """
            INSERT OR IGNORE INTO workspace_members (workspace_id, user_id, role, joined_at)
            VALUES ($wid, $uid, 'owner', $joined)
            """;
        memberCmd.Parameters.AddWithValue("$wid", workspace.WorkspaceId);
        memberCmd.Parameters.AddWithValue("$uid", userId);
        memberCmd.Parameters.AddWithValue("$joined", Ts(workspace.CreatedAt));
        await memberCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return workspace;
    }

    public async Task<Workspace> CreateTeamWorkspaceAsync(string name, string slug, string ownerId, CancellationToken ct = default)
    {
        var workspace = new Workspace
        {
            WorkspaceId = $"ws-{Guid.NewGuid():N}",
            Type = WorkspaceType.Team,
            Name = name,
            Slug = slug,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO workspaces (workspace_id, type, name, slug, owner_id, created_at, updated_at)
            VALUES ($wid, $type, $name, $slug, $owner, $created, $updated)
            """;
        cmd.Parameters.AddWithValue("$wid", workspace.WorkspaceId);
        cmd.Parameters.AddWithValue("$type", "team");
        cmd.Parameters.AddWithValue("$name", workspace.Name);
        cmd.Parameters.AddWithValue("$slug", workspace.Slug);
        cmd.Parameters.AddWithValue("$owner", workspace.OwnerId);
        cmd.Parameters.AddWithValue("$created", Ts(workspace.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", Ts(workspace.UpdatedAt));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Add owner as member.
        using var memberCmd = connection.CreateCommand();
        memberCmd.CommandText = """
            INSERT INTO workspace_members (workspace_id, user_id, role, joined_at)
            VALUES ($wid, $uid, 'owner', $joined)
            """;
        memberCmd.Parameters.AddWithValue("$wid", workspace.WorkspaceId);
        memberCmd.Parameters.AddWithValue("$uid", ownerId);
        memberCmd.Parameters.AddWithValue("$joined", Ts(workspace.CreatedAt));
        await memberCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return workspace;
    }

    public async Task<Workspace?> GetAsync(string workspaceId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT workspace_id, type, name, slug, owner_id, created_at, updated_at
            FROM workspaces WHERE workspace_id = $wid
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? ReadWorkspace(reader) : null;
    }

    public async Task<Workspace?> GetPersonalAsync(string userId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT workspace_id, type, name, slug, owner_id, created_at, updated_at
            FROM workspaces WHERE owner_id = $uid AND type = 'personal'
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$uid", userId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? ReadWorkspace(reader) : null;
    }

    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(string userId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT w.workspace_id, w.type, w.name, w.slug, w.owner_id, w.created_at, w.updated_at
            FROM workspaces w
            INNER JOIN workspace_members wm ON w.workspace_id = wm.workspace_id
            WHERE wm.user_id = $uid
            ORDER BY w.type ASC, w.name ASC
            """;
        cmd.Parameters.AddWithValue("$uid", userId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<Workspace>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(ReadWorkspace(reader));
        return results;
    }

    public async Task<Workspace?> UpdateAsync(string workspaceId, string? name, string? slug, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE workspaces
            SET name = COALESCE($name, name),
                slug = COALESCE($slug, slug),
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE workspace_id = $wid
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        cmd.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$slug", (object?)slug ?? DBNull.Value);
        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return affected > 0 ? await GetAsync(workspaceId, ct).ConfigureAwait(false) : null;
    }

    public async Task<bool> DeleteAsync(string workspaceId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Verify it's a team workspace — personal workspaces cannot be deleted.
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT type FROM workspaces WHERE workspace_id = $wid";
        check.Parameters.AddWithValue("$wid", workspaceId);
        var type = await check.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
        if (type is null or "personal")
            return false;

        // Delete members, invites, config, memory, then workspace.
        using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        foreach (var table in new[] { "workspace_memory", "workspace_invites", "workspace_config", "workspace_members" })
        {
            using var del = connection.CreateCommand();
            del.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
#pragma warning disable CA2100 // Table names are from a static array, not user input
            del.CommandText = $"DELETE FROM {table} WHERE workspace_id = $wid";
#pragma warning restore CA2100
            del.Parameters.AddWithValue("$wid", workspaceId);
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        using var delWs = connection.CreateCommand();
        delWs.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
        delWs.CommandText = "DELETE FROM workspaces WHERE workspace_id = $wid";
        delWs.Parameters.AddWithValue("$wid", workspaceId);
        await delWs.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return true;
    }

    // ── Membership ─────────────────────────────────────────────────────────

    public async Task<bool> IsMemberAsync(string workspaceId, string userId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM workspace_members
            WHERE workspace_id = $wid AND user_id = $uid
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        cmd.Parameters.AddWithValue("$uid", userId);
        return await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null;
    }

    public async Task<WorkspaceRole?> GetMemberRoleAsync(string workspaceId, string userId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT role FROM workspace_members
            WHERE workspace_id = $wid AND user_id = $uid
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        cmd.Parameters.AddWithValue("$uid", userId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
        return result is not null && Enum.TryParse<WorkspaceRole>(result, ignoreCase: true, out var role) ? role : null;
    }

    public async Task<IReadOnlyList<WorkspaceMember>> ListMembersAsync(string workspaceId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT workspace_id, user_id, role, joined_at
            FROM workspace_members WHERE workspace_id = $wid
            ORDER BY joined_at ASC
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<WorkspaceMember>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new WorkspaceMember
            {
                WorkspaceId = reader.GetString(0),
                UserId = reader.GetString(1),
                Role = Enum.TryParse<WorkspaceRole>(reader.GetString(2), ignoreCase: true, out var r) ? r : WorkspaceRole.Member,
                JoinedAt = ParseTs(reader.GetString(3)),
            });
        }
        return results;
    }

    public async Task AddMemberAsync(string workspaceId, string userId, WorkspaceRole role, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO workspace_members (workspace_id, user_id, role, joined_at)
            VALUES ($wid, $uid, $role, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$role", role.ToString().ToLowerInvariant());
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> RemoveMemberAsync(string workspaceId, string userId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Cannot remove the owner.
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT role FROM workspace_members WHERE workspace_id = $wid AND user_id = $uid";
        check.Parameters.AddWithValue("$wid", workspaceId);
        check.Parameters.AddWithValue("$uid", userId);
        var role = await check.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
        if (role is null || string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase))
            return false;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM workspace_members WHERE workspace_id = $wid AND user_id = $uid";
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        cmd.Parameters.AddWithValue("$uid", userId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    // ── Invites ────────────────────────────────────────────────────────────

    public async Task<WorkspaceInvite> CreateInviteAsync(string workspaceId, string email, WorkspaceRole role, CancellationToken ct = default)
    {
        var invite = new WorkspaceInvite
        {
            InviteId = $"inv-{Guid.NewGuid():N}",
            WorkspaceId = workspaceId,
            Email = email,
            Role = role,
            Token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO workspace_invites (invite_id, workspace_id, email, role, token, expires_at, created_at)
            VALUES ($id, $wid, $email, $role, $token, $expires, $created)
            """;
        cmd.Parameters.AddWithValue("$id", invite.InviteId);
        cmd.Parameters.AddWithValue("$wid", invite.WorkspaceId);
        cmd.Parameters.AddWithValue("$email", invite.Email);
        cmd.Parameters.AddWithValue("$role", invite.Role.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$token", invite.Token);
        cmd.Parameters.AddWithValue("$expires", Ts(invite.ExpiresAt));
        cmd.Parameters.AddWithValue("$created", Ts(invite.CreatedAt));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return invite;
    }

    public async Task<bool> AcceptInviteAsync(string token, string userId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();

        // Find the invite.
        using var find = connection.CreateCommand();
        find.CommandText = """
            SELECT invite_id, workspace_id, role, expires_at, accepted_at
            FROM workspace_invites WHERE token = $token
            """;
        find.Parameters.AddWithValue("$token", token);
        using var reader = await find.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return false;

        var inviteId = reader.GetString(0);
        var workspaceId = reader.GetString(1);
        var role = reader.GetString(2);
        var expiresAt = ParseTs(reader.GetString(3));
        var acceptedAt = await reader.IsDBNullAsync(4, ct).ConfigureAwait(false) ? (DateTimeOffset?)null : ParseTs(reader.GetString(4));
        await reader.CloseAsync().ConfigureAwait(false);

        // Already accepted or expired.
        if (acceptedAt.HasValue || expiresAt < DateTimeOffset.UtcNow)
            return false;

        using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Mark accepted.
        using var accept = connection.CreateCommand();
        accept.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
        accept.CommandText = """
            UPDATE workspace_invites
            SET accepted_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE invite_id = $id
            """;
        accept.Parameters.AddWithValue("$id", inviteId);
        await accept.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Add as member.
        using var addMember = connection.CreateCommand();
        addMember.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
        addMember.CommandText = """
            INSERT OR IGNORE INTO workspace_members (workspace_id, user_id, role, joined_at)
            VALUES ($wid, $uid, $role, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        addMember.Parameters.AddWithValue("$wid", workspaceId);
        addMember.Parameters.AddWithValue("$uid", userId);
        addMember.Parameters.AddWithValue("$role", role);
        await addMember.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteInviteAsync(string inviteId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM workspace_invites WHERE invite_id = $id";
        cmd.Parameters.AddWithValue("$id", inviteId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<WorkspaceInvite>> ListInvitesAsync(string workspaceId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT invite_id, workspace_id, email, role, token, expires_at, accepted_at, created_at
            FROM workspace_invites WHERE workspace_id = $wid
            ORDER BY created_at DESC
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<WorkspaceInvite>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new WorkspaceInvite
            {
                InviteId = reader.GetString(0),
                WorkspaceId = reader.GetString(1),
                Email = reader.GetString(2),
                Role = Enum.TryParse<WorkspaceRole>(reader.GetString(3), ignoreCase: true, out var r) ? r : WorkspaceRole.Member,
                Token = reader.GetString(4),
                ExpiresAt = ParseTs(reader.GetString(5)),
                AcceptedAt = await reader.IsDBNullAsync(6, ct).ConfigureAwait(false) ? null : ParseTs(reader.GetString(6)),
                CreatedAt = ParseTs(reader.GetString(7)),
            });
        }
        return results;
    }

    // ── Config ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyDictionary<string, string>> GetConfigAsync(string workspaceId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM workspace_config WHERE workspace_id = $wid";
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new Dictionary<string, string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results[reader.GetString(0)] = reader.GetString(1);
        return results;
    }

    public async Task SetConfigAsync(string workspaceId, IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        foreach (var (key, value) in values)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
            cmd.CommandText = """
                INSERT INTO workspace_config (workspace_id, key, value)
                VALUES ($wid, $key, $val)
                ON CONFLICT(workspace_id, key) DO UPDATE SET value = $val
                """;
            cmd.Parameters.AddWithValue("$wid", workspaceId);
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$val", value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    // ── Usage ──────────────────────────────────────────────────────────────

    public async Task<WorkspaceUsage> GetUsageAsync(string workspaceId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(input_tokens), 0),
                   COALESCE(SUM(output_tokens), 0),
                   COALESCE(SUM(cost_usd), 0.0),
                   COUNT(DISTINCT session_id)
            FROM token_usage WHERE workspace_id = $wid
            """;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return new WorkspaceUsage();

        return new WorkspaceUsage
        {
            TotalInputTokens = reader.GetInt64(0),
            TotalOutputTokens = reader.GetInt64(1),
            TotalCostUsd = reader.GetDouble(2),
            SessionCount = reader.GetInt32(3),
        };
    }

    // ── Memory ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WorkspaceMemoryEntry>> ListMemoryAsync(string workspaceId, string? layer = null, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        var sql = """
            SELECT memory_id, workspace_id, layer, content, confidence, project_id, created_at, updated_at
            FROM workspace_memory WHERE workspace_id = $wid
            """;
        if (layer is not null)
            sql += " AND layer = $layer";
        sql += " ORDER BY updated_at DESC";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        if (layer is not null)
            cmd.Parameters.AddWithValue("$layer", layer);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<WorkspaceMemoryEntry>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new WorkspaceMemoryEntry
            {
                MemoryId = reader.GetString(0),
                WorkspaceId = reader.GetString(1),
                Layer = reader.GetString(2),
                Content = reader.GetString(3),
                Confidence = await reader.IsDBNullAsync(4, ct).ConfigureAwait(false) ? null : reader.GetDouble(4),
                ProjectId = await reader.IsDBNullAsync(5, ct).ConfigureAwait(false) ? null : reader.GetString(5),
                CreatedAt = ParseTs(reader.GetString(6)),
                UpdatedAt = ParseTs(reader.GetString(7)),
            });
        }
        return results;
    }

    public async Task SaveMemoryAsync(WorkspaceMemoryEntry entry, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO workspace_memory (memory_id, workspace_id, layer, content, confidence, project_id, created_at, updated_at)
            VALUES ($mid, $wid, $layer, $content, $conf, $proj, $created, $updated)
            ON CONFLICT(memory_id) DO UPDATE SET
                content = $content, confidence = $conf, project_id = $proj,
                updated_at = $updated
            """;
        cmd.Parameters.AddWithValue("$mid", entry.MemoryId);
        cmd.Parameters.AddWithValue("$wid", entry.WorkspaceId);
        cmd.Parameters.AddWithValue("$layer", entry.Layer);
        cmd.Parameters.AddWithValue("$content", entry.Content);
        cmd.Parameters.AddWithValue("$conf", entry.Confidence.HasValue ? entry.Confidence.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$proj", (object?)entry.ProjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", Ts(entry.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", Ts(entry.UpdatedAt));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteMemoryAsync(string memoryId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM workspace_memory WHERE memory_id = $mid";
        cmd.Parameters.AddWithValue("$mid", memoryId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Workspace ReadWorkspace(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
    {
        WorkspaceId = reader.GetString(0),
        Type = Enum.TryParse<WorkspaceType>(reader.GetString(1), ignoreCase: true, out var t) ? t : WorkspaceType.Personal,
        Name = reader.GetString(2),
        Slug = reader.GetString(3),
        OwnerId = reader.GetString(4),
        CreatedAt = ParseTs(reader.GetString(5)),
        UpdatedAt = ParseTs(reader.GetString(6)),
    };

    private static string Ts(DateTimeOffset dto) =>
        dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTs(string s) =>
        DateTimeOffset.Parse(s, CultureInfo.InvariantCulture);
}
