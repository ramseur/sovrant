using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Projects;

/// <summary>SQLite-backed implementation of <see cref="IProjectService"/>.</summary>
[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Database stores enum values as lowercase by convention.")]
internal sealed class SqliteProjectStore : IProjectService
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IWorkspaceService _workspaceService;

    public SqliteProjectStore(ISqliteConnectionFactory connectionFactory, IWorkspaceService workspaceService)
    {
        _connectionFactory = connectionFactory;
        _workspaceService = workspaceService;
    }

    // ── Project CRUD ───────────────────────────────────────────────────────

    public async Task<Project> CreateAsync(string workspaceId, string name, string slug, string? description = null, CancellationToken ct = default)
    {
        var project = new Project
        {
            ProjectId = $"proj-{Guid.NewGuid():N}",
            WorkspaceId = workspaceId,
            Name = name,
            Slug = slug,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO projects (project_id, workspace_id, name, slug, description, created_at, updated_at)
            VALUES ($pid, $wid, $name, $slug, $desc, $created, $updated)
            """;
        cmd.Parameters.AddWithValue("$pid", project.ProjectId);
        cmd.Parameters.AddWithValue("$wid", project.WorkspaceId);
        cmd.Parameters.AddWithValue("$name", project.Name);
        cmd.Parameters.AddWithValue("$slug", project.Slug);
        cmd.Parameters.AddWithValue("$desc", (object?)project.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", Ts(project.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", Ts(project.UpdatedAt));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return project;
    }

    public async Task<Project?> GetAsync(string projectId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT project_id, workspace_id, name, slug, description, created_at, updated_at, archived_at
            FROM projects WHERE project_id = $pid
            """;
        cmd.Parameters.AddWithValue("$pid", projectId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? await ReadProjectAsync(reader, ct).ConfigureAwait(false) : null;
    }

    public async Task<IReadOnlyList<Project>> ListAsync(string workspaceId, bool includeArchived = false, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        var sql = "SELECT project_id, workspace_id, name, slug, description, created_at, updated_at, archived_at FROM projects WHERE workspace_id = $wid";
        if (!includeArchived)
            sql += " AND archived_at IS NULL";
        sql += " ORDER BY name ASC";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$wid", workspaceId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<Project>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(await ReadProjectAsync(reader, ct).ConfigureAwait(false));
        return results;
    }

    public async Task<Project?> UpdateAsync(string projectId, string? name = null, string? slug = null, string? description = null, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE projects
            SET name = COALESCE($name, name),
                slug = COALESCE($slug, slug),
                description = COALESCE($desc, description),
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE project_id = $pid
            """;
        cmd.Parameters.AddWithValue("$pid", projectId);
        cmd.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$slug", (object?)slug ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$desc", (object?)description ?? DBNull.Value);
        var affected = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return affected > 0 ? await GetAsync(projectId, ct).ConfigureAwait(false) : null;
    }

    public async Task<bool> ArchiveAsync(string projectId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE projects
            SET archived_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE project_id = $pid AND archived_at IS NULL
            """;
        cmd.Parameters.AddWithValue("$pid", projectId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<bool> UnarchiveAsync(string projectId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE projects
            SET archived_at = NULL,
                updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
            WHERE project_id = $pid AND archived_at IS NOT NULL
            """;
        cmd.Parameters.AddWithValue("$pid", projectId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<bool> DeleteAsync(string projectId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Verify project exists.
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT workspace_id FROM projects WHERE project_id = $pid";
        check.Parameters.AddWithValue("$pid", projectId);
        if (await check.ExecuteScalarAsync(ct).ConfigureAwait(false) is null)
            return false;

        using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Delete project-scoped workspace memory.
        using var delMem = connection.CreateCommand();
        delMem.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
        delMem.CommandText = "DELETE FROM workspace_memory WHERE project_id = $pid";
        delMem.Parameters.AddWithValue("$pid", projectId);
        await delMem.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Delete project config, members, then project.
        foreach (var table in new[] { "project_config", "project_members" })
        {
            using var del = connection.CreateCommand();
            del.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
#pragma warning disable CA2100 // Table names are from a static span, not user input
            del.CommandText = $"DELETE FROM {table} WHERE project_id = $pid";
#pragma warning restore CA2100
            del.Parameters.AddWithValue("$pid", projectId);
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        using var delProj = connection.CreateCommand();
        delProj.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
        delProj.CommandText = "DELETE FROM projects WHERE project_id = $pid";
        delProj.Parameters.AddWithValue("$pid", projectId);
        await delProj.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return true;
    }

    // ── Membership ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProjectMember>> ListMembersAsync(string projectId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT project_id, user_id, role, joined_at
            FROM project_members WHERE project_id = $pid
            ORDER BY joined_at ASC
            """;
        cmd.Parameters.AddWithValue("$pid", projectId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<ProjectMember>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new ProjectMember
            {
                ProjectId = reader.GetString(0),
                UserId = reader.GetString(1),
                Role = Enum.TryParse<ProjectRole>(reader.GetString(2), ignoreCase: true, out var r) ? r : ProjectRole.Contributor,
                JoinedAt = ParseTs(reader.GetString(3)),
            });
        }
        return results;
    }

    public async Task<bool> AddMemberAsync(string projectId, string userId, ProjectRole role, CancellationToken ct = default)
    {
        // Verify user is a workspace member first.
        var project = await GetAsync(projectId, ct).ConfigureAwait(false);
        if (project is null)
            return false;

        var isMember = await _workspaceService.IsMemberAsync(project.WorkspaceId, userId, ct).ConfigureAwait(false);
        if (!isMember)
            return false;

        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO project_members (project_id, user_id, role, joined_at)
            VALUES ($pid, $uid, $role, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        cmd.Parameters.AddWithValue("$pid", projectId);
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$role", role.ToString().ToLowerInvariant());
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RemoveMemberAsync(string projectId, string userId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM project_members WHERE project_id = $pid AND user_id = $uid";
        cmd.Parameters.AddWithValue("$pid", projectId);
        cmd.Parameters.AddWithValue("$uid", userId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<bool> HasAccessAsync(string projectId, string userId, CancellationToken ct = default)
    {
        var project = await GetAsync(projectId, ct).ConfigureAwait(false);
        if (project is null)
            return false;

        // Must be a workspace member.
        var isWsMember = await _workspaceService.IsMemberAsync(project.WorkspaceId, userId, ct).ConfigureAwait(false);
        if (!isWsMember)
            return false;

        // If no explicit project members, all workspace members have access (open by default).
        using var connection = _connectionFactory.CreateConnection();
        using var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM project_members WHERE project_id = $pid";
        countCmd.Parameters.AddWithValue("$pid", projectId);
        var memberCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (memberCount == 0)
            return true;

        // Check explicit membership.
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT 1 FROM project_members WHERE project_id = $pid AND user_id = $uid LIMIT 1";
        checkCmd.Parameters.AddWithValue("$pid", projectId);
        checkCmd.Parameters.AddWithValue("$uid", userId);
        return await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null;
    }

    // ── Config ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyDictionary<string, string>> GetConfigAsync(string projectId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM project_config WHERE project_id = $pid";
        cmd.Parameters.AddWithValue("$pid", projectId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new Dictionary<string, string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results[reader.GetString(0)] = reader.GetString(1);
        return results;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetResolvedConfigAsync(string projectId, CancellationToken ct = default)
    {
        var project = await GetAsync(projectId, ct).ConfigureAwait(false);
        if (project is null)
            return new Dictionary<string, string>();

        // Layer 3: global defaults from config table.
        var resolved = new Dictionary<string, string>();
        using var connection = _connectionFactory.CreateConnection();
        using var globalCmd = connection.CreateCommand();
        globalCmd.CommandText = "SELECT key, value FROM config WHERE scope = 'global'";
        using var globalReader = await globalCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await globalReader.ReadAsync(ct).ConfigureAwait(false))
            resolved[globalReader.GetString(0)] = globalReader.GetString(1);

        // Layer 2: workspace config overrides global.
        var wsConfig = await _workspaceService.GetConfigAsync(project.WorkspaceId, ct).ConfigureAwait(false);
        foreach (var (key, value) in wsConfig)
            resolved[key] = value;

        // Layer 1: project config overrides workspace.
        var projConfig = await GetConfigAsync(projectId, ct).ConfigureAwait(false);
        foreach (var (key, value) in projConfig)
            resolved[key] = value;

        return resolved;
    }

    public async Task SetConfigAsync(string projectId, IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        foreach (var (key, value) in values)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;
            cmd.CommandText = """
                INSERT INTO project_config (project_id, key, value)
                VALUES ($pid, $key, $val)
                ON CONFLICT(project_id, key) DO UPDATE SET value = $val
                """;
            cmd.Parameters.AddWithValue("$pid", projectId);
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$val", value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    // ── Sessions ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> ListSessionsAsync(string projectId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_id FROM sessions
            WHERE project_id = $pid
            ORDER BY started_at DESC
            """;
        cmd.Parameters.AddWithValue("$pid", projectId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(reader.GetString(0));
        return results;
    }

    // ── Usage ──────────────────────────────────────────────────────────────

    public async Task<ProjectUsage> GetUsageAsync(string projectId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(input_tokens), 0),
                   COALESCE(SUM(output_tokens), 0),
                   COALESCE(SUM(cost_usd), 0.0),
                   COUNT(DISTINCT session_id)
            FROM token_usage WHERE project_id = $pid
            """;
        cmd.Parameters.AddWithValue("$pid", projectId);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return new ProjectUsage();

        return new ProjectUsage
        {
            TotalInputTokens = reader.GetInt64(0),
            TotalOutputTokens = reader.GetInt64(1),
            TotalCostUsd = reader.GetDouble(2),
            SessionCount = reader.GetInt32(3),
        };
    }

    // ── Memory ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WorkspaceMemoryEntry>> GetMemoryAsync(string projectId, string? layer = null, CancellationToken ct = default)
    {
        var project = await GetAsync(projectId, ct).ConfigureAwait(false);
        if (project is null)
            return [];

        // Merge: project-scoped memory + workspace-level memory (project_id IS NULL).
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        var sql = """
            SELECT memory_id, workspace_id, layer, content, confidence, project_id, created_at, updated_at
            FROM workspace_memory
            WHERE workspace_id = $wid AND (project_id = $pid OR project_id IS NULL)
            """;
        if (layer is not null)
            sql += " AND layer = $layer";
        sql += " ORDER BY updated_at DESC";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$wid", project.WorkspaceId);
        cmd.Parameters.AddWithValue("$pid", projectId);
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

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<Project> ReadProjectAsync(Microsoft.Data.Sqlite.SqliteDataReader reader, CancellationToken ct) => new()
    {
        ProjectId = reader.GetString(0),
        WorkspaceId = reader.GetString(1),
        Name = reader.GetString(2),
        Slug = reader.GetString(3),
        Description = await reader.IsDBNullAsync(4, ct).ConfigureAwait(false) ? null : reader.GetString(4),
        CreatedAt = ParseTs(reader.GetString(5)),
        UpdatedAt = ParseTs(reader.GetString(6)),
        ArchivedAt = await reader.IsDBNullAsync(7, ct).ConfigureAwait(false) ? null : ParseTs(reader.GetString(7)),
    };

    private static string Ts(DateTimeOffset dto) =>
        dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTs(string s) =>
        DateTimeOffset.Parse(s, CultureInfo.InvariantCulture);
}
