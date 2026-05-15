using System.Globalization;
using System.Text;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 51 — SQLite-backed <see cref="IMissionScratchpadStore"/>. Writes
/// to the <c>mission_scratchpad</c> table (V010). Append-only: every
/// write produces a new row, and readers decide whether they want the
/// full history or only the latest value per key.
/// </summary>
internal sealed class SqliteMissionScratchpadStore(ISqliteConnectionFactory connectionFactory) : IMissionScratchpadStore
{
    public async Task AppendAsync(MissionScratchpadEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mission_scratchpad
                (mission_id, step_index, agent_id, namespace, key, value,
                 workspace_id, project_id)
            VALUES
                ($missionId, $stepIndex, $agentId, $namespace, $key, $value,
                 $workspaceId, $projectId)
            """;
        cmd.Parameters.AddWithValue("$missionId", entry.MissionId);
        cmd.Parameters.AddWithValue("$stepIndex", entry.StepIndex);
        cmd.Parameters.AddWithValue("$agentId", (object?)entry.AgentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$namespace", entry.Namespace);
        cmd.Parameters.AddWithValue("$key", entry.Key);
        cmd.Parameters.AddWithValue("$value", entry.Value);
        cmd.Parameters.AddWithValue("$workspaceId", (object?)entry.WorkspaceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$projectId", (object?)entry.ProjectId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MissionScratchpadEntry>> LoadAsync(
        string missionId,
        int? stepIndex = null,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(missionId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();

        var sql = new StringBuilder("""
            SELECT mission_id, step_index, agent_id, namespace, key, value,
                   created_at, workspace_id, project_id
            FROM mission_scratchpad
            WHERE mission_id = $missionId
            """);
        cmd.Parameters.AddWithValue("$missionId", missionId);

        if (stepIndex.HasValue)
        {
            sql.Append(" AND step_index = $stepIndex");
            cmd.Parameters.AddWithValue("$stepIndex", stepIndex.Value);
        }
        if (@namespace is not null)
        {
            sql.Append(" AND namespace = $namespace");
            cmd.Parameters.AddWithValue("$namespace", @namespace);
        }
        sql.Append(" ORDER BY id ASC");
#pragma warning disable CA2100 // SQL is built from constant fragments only — no user input interpolated
        cmd.CommandText = sql.ToString();
#pragma warning restore CA2100

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<MissionScratchpadEntry>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(await ReadRowAsync(reader, ct).ConfigureAwait(false));
        }
        return results;
    }

    public async Task<MissionScratchpadEntry?> ReadLatestAsync(
        string missionId,
        string @namespace,
        string key,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(missionId);
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(key);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT mission_id, step_index, agent_id, namespace, key, value,
                   created_at, workspace_id, project_id
            FROM mission_scratchpad
            WHERE mission_id = $missionId
              AND namespace  = $namespace
              AND key        = $key
            ORDER BY id DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$missionId", missionId);
        cmd.Parameters.AddWithValue("$namespace", @namespace);
        cmd.Parameters.AddWithValue("$key", key);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return await ReadRowAsync(reader, ct).ConfigureAwait(false);
    }

    public async Task<int> DeleteMissionAsync(string missionId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(missionId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM mission_scratchpad WHERE mission_id = $missionId";
        cmd.Parameters.AddWithValue("$missionId", missionId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<MissionScratchpadEntry> ReadRowAsync(
        System.Data.Common.DbDataReader reader, CancellationToken ct)
    {
        return new MissionScratchpadEntry(
            MissionId: reader.GetString(0),
            StepIndex: reader.GetInt32(1),
            AgentId: await reader.IsDBNullAsync(2, ct).ConfigureAwait(false) ? null : reader.GetString(2),
            Namespace: reader.GetString(3),
            Key: reader.GetString(4),
            Value: reader.GetString(5),
            CreatedAt: DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
            WorkspaceId: await reader.IsDBNullAsync(7, ct).ConfigureAwait(false) ? null : reader.GetString(7),
            ProjectId: await reader.IsDBNullAsync(8, ct).ConfigureAwait(false) ? null : reader.GetString(8));
    }
}
