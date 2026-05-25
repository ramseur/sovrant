using System.Globalization;
using System.Text;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed swarm event store. Writes to the <c>swarm_events</c> table
/// (created in V005, indexed for workspace/project lookups in V006/V007).
/// </summary>
internal sealed class SqliteSwarmEventStore(ISqliteConnectionFactory connectionFactory) : ISwarmEventStore
{
    public async Task RecordEventAsync(SwarmEventRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO swarm_events
                (swarm_id, event_type, agent_id, workspace_id, project_id, user_id, parent_swarm_id, payload, timestamp)
            VALUES
                ($swarmId, $eventType, $agentId, $workspaceId, $projectId, $userId, $parentSwarmId, $payload, $timestamp)
            """;
        cmd.Parameters.AddWithValue("$swarmId", record.SwarmId);
        cmd.Parameters.AddWithValue("$eventType", record.EventType);
        cmd.Parameters.AddWithValue("$agentId", (object?)record.AgentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$workspaceId", (object?)record.WorkspaceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$projectId", (object?)record.ProjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$userId", (object?)record.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$parentSwarmId", (object?)record.ParentSwarmId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$payload", record.Payload);
        cmd.Parameters.AddWithValue("$timestamp", record.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SwarmEventRecord>> LoadEventsAsync(string swarmId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(swarmId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT swarm_id, event_type, agent_id, workspace_id, project_id, user_id, parent_swarm_id, payload, timestamp
            FROM swarm_events
            WHERE swarm_id = $swarmId
            ORDER BY id ASC
            """;
        cmd.Parameters.AddWithValue("$swarmId", swarmId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<SwarmEventRecord>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new SwarmEventRecord(
                SwarmId: reader.GetString(0),
                EventType: reader.GetString(1),
                AgentId: await reader.IsDBNullAsync(2, ct).ConfigureAwait(false) ? null : reader.GetString(2),
                WorkspaceId: await reader.IsDBNullAsync(3, ct).ConfigureAwait(false) ? null : reader.GetString(3),
                ProjectId: await reader.IsDBNullAsync(4, ct).ConfigureAwait(false) ? null : reader.GetString(4),
                UserId: await reader.IsDBNullAsync(5, ct).ConfigureAwait(false) ? null : reader.GetString(5),
                ParentSwarmId: await reader.IsDBNullAsync(6, ct).ConfigureAwait(false) ? null : reader.GetString(6),
                Payload: reader.GetString(7),
                Timestamp: DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture)));
        }
        return results;
    }

    public async Task<IReadOnlyList<string>> ListSwarmsAsync(SwarmListFilter? filter = null, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();

        var sql = new StringBuilder("""
            SELECT swarm_id, MAX(timestamp) AS last_ts
            FROM swarm_events
            """);

        var clauses = new List<string>();
        if (filter?.WorkspaceId is not null)
        {
            clauses.Add("workspace_id = $workspaceId");
            cmd.Parameters.AddWithValue("$workspaceId", filter.WorkspaceId);
        }
        if (filter?.ProjectId is not null)
        {
            clauses.Add("project_id = $projectId");
            cmd.Parameters.AddWithValue("$projectId", filter.ProjectId);
        }
        if (filter?.ParentSwarmId is not null)
        {
            clauses.Add("parent_swarm_id = $parentSwarmId");
            cmd.Parameters.AddWithValue("$parentSwarmId", filter.ParentSwarmId);
        }
        if (clauses.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", clauses));
        }
        sql.Append(" GROUP BY swarm_id ORDER BY last_ts DESC");
        if (filter?.Limit is int limit && limit > 0)
        {
            sql.Append(" LIMIT $limit");
            cmd.Parameters.AddWithValue("$limit", limit);
        }

#pragma warning disable CA2100 // SQL is built from string literals only; values flow exclusively through parameters
        cmd.CommandText = sql.ToString();
#pragma warning restore CA2100

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }

    public async Task<bool> ExistsAsync(string swarmId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(swarmId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM swarm_events WHERE swarm_id = $swarmId LIMIT 1";
        cmd.Parameters.AddWithValue("$swarmId", swarmId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is not null;
    }

    public async Task<int> DeleteSwarmAsync(string swarmId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(swarmId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM swarm_events WHERE swarm_id = $swarmId";
        cmd.Parameters.AddWithValue("$swarmId", swarmId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<string?> GetOwnerAsync(string swarmId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(swarmId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT user_id FROM swarm_events
            WHERE swarm_id = $swarmId
            ORDER BY id ASC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$swarmId", swarmId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is DBNull or null ? null : (string)result;
    }

    public async Task<IReadOnlyList<string>> ListChildrenAsync(string parentSwarmId, int? limit = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parentSwarmId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();

        var sql = new StringBuilder("""
            SELECT swarm_id, MAX(timestamp) AS last_ts
            FROM swarm_events
            WHERE parent_swarm_id = $parentSwarmId
            GROUP BY swarm_id
            ORDER BY last_ts DESC
            """);

        if (limit is int lim && lim > 0)
        {
            sql.Append(" LIMIT $limit");
            cmd.Parameters.AddWithValue("$limit", lim);
        }

#pragma warning disable CA2100
        cmd.CommandText = sql.ToString();
#pragma warning restore CA2100
        cmd.Parameters.AddWithValue("$parentSwarmId", parentSwarmId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(reader.GetString(0));
        return results;
    }
}
