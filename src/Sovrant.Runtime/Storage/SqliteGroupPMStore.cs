namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed group PM assignment store. Reads/writes the
/// <c>group_pm_assignments</c> table created in V013.
/// </summary>
internal sealed class SqliteGroupPMStore(ISqliteConnectionFactory connectionFactory) : IGroupPMStore
{
    public async Task UpsertAsync(GroupPMAssignment assignment, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO group_pm_assignments (group_id, group_type, pm_template, workspace_id, project_id)
            VALUES ($groupId, $groupType, $pmTemplate, $workspaceId, $projectId)
            ON CONFLICT(group_id) DO UPDATE SET
                group_type  = excluded.group_type,
                pm_template = excluded.pm_template,
                workspace_id = excluded.workspace_id,
                project_id  = excluded.project_id
            """;
        cmd.Parameters.AddWithValue("$groupId", assignment.GroupId);
        cmd.Parameters.AddWithValue("$groupType", assignment.GroupType);
        cmd.Parameters.AddWithValue("$pmTemplate", assignment.PMTemplate);
        cmd.Parameters.AddWithValue("$workspaceId", assignment.WorkspaceId);
        cmd.Parameters.AddWithValue("$projectId", (object?)assignment.ProjectId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<GroupPMAssignment?> GetAsync(string groupId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(groupId);

        using var connection = connectionFactory.CreateReadOnlyConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT group_id, group_type, pm_template, workspace_id, project_id, created_at
            FROM group_pm_assignments
            WHERE group_id = $groupId
            """;
        cmd.Parameters.AddWithValue("$groupId", groupId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return ReadAssignment(reader);
    }

    public async Task<IReadOnlyList<GroupPMAssignment>> ListByWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);

        using var connection = connectionFactory.CreateReadOnlyConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT group_id, group_type, pm_template, workspace_id, project_id, created_at
            FROM group_pm_assignments
            WHERE workspace_id = $workspaceId
            ORDER BY created_at ASC
            """;
        cmd.Parameters.AddWithValue("$workspaceId", workspaceId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<GroupPMAssignment>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(ReadAssignment(reader));
        }
        return results;
    }

    public async Task DeleteAsync(string groupId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(groupId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM group_pm_assignments WHERE group_id = $groupId";
        cmd.Parameters.AddWithValue("$groupId", groupId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static GroupPMAssignment ReadAssignment(Microsoft.Data.Sqlite.SqliteDataReader reader) =>
        new(
            GroupId: reader.GetString(0),
            GroupType: reader.GetString(1),
            PMTemplate: reader.GetString(2),
            WorkspaceId: reader.GetString(3),
            ProjectId: reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt: reader.GetString(5));
}
