using System.Globalization;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed coordination event store. Reads/writes the <c>coordination_events</c>
/// table created in V013.
/// </summary>
internal sealed class SqliteCoordinationEventStore(ISqliteConnectionFactory connectionFactory) : ICoordinationEventStore
{
    public async Task<long> EnqueueAsync(CoordinationEventRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO coordination_events
                (source_group_id, target_group_id, event_type, payload, status, workspace_id, project_id)
            VALUES
                ($sourceGroupId, $targetGroupId, $eventType, $payload, $status, $workspaceId, $projectId);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$sourceGroupId", record.SourceGroupId);
        cmd.Parameters.AddWithValue("$targetGroupId", record.TargetGroupId);
        cmd.Parameters.AddWithValue("$eventType", record.EventType);
        cmd.Parameters.AddWithValue("$payload", record.Payload);
        cmd.Parameters.AddWithValue("$status", record.Status);
        cmd.Parameters.AddWithValue("$workspaceId", record.WorkspaceId);
        cmd.Parameters.AddWithValue("$projectId", (object?)record.ProjectId ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<CoordinationEventRecord>> GetPendingAsync(string targetGroupId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(targetGroupId);

        using var connection = connectionFactory.CreateReadOnlyConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, source_group_id, target_group_id, event_type, payload, status,
                   workspace_id, project_id, created_at, delivered_at, acknowledged_at
            FROM coordination_events
            WHERE target_group_id = $targetGroupId AND status = 'pending'
            ORDER BY id ASC
            """;
        cmd.Parameters.AddWithValue("$targetGroupId", targetGroupId);

        return await ReadRecordsAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task MarkDeliveredAsync(long eventId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE coordination_events
            SET status = 'delivered', delivered_at = datetime('now')
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", eventId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkAcknowledgedAsync(long eventId, CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE coordination_events
            SET status = 'acknowledged', acknowledged_at = datetime('now')
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", eventId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CoordinationEventRecord>> ListByGroupAsync(string groupId, int limit = 50, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(groupId);

        using var connection = connectionFactory.CreateReadOnlyConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, source_group_id, target_group_id, event_type, payload, status,
                   workspace_id, project_id, created_at, delivered_at, acknowledged_at
            FROM coordination_events
            WHERE source_group_id = $groupId OR target_group_id = $groupId
            ORDER BY id DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$groupId", groupId);
        cmd.Parameters.AddWithValue("$limit", limit);

        return await ReadRecordsAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<int> GetPendingCountAsync(string targetGroupId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(targetGroupId);

        using var connection = connectionFactory.CreateReadOnlyConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM coordination_events
            WHERE target_group_id = $targetGroupId AND status = 'pending'
            """;
        cmd.Parameters.AddWithValue("$targetGroupId", targetGroupId);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<CoordinationEventRecord>> ReadRecordsAsync(
        Microsoft.Data.Sqlite.SqliteCommand cmd, CancellationToken ct)
    {
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<CoordinationEventRecord>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new CoordinationEventRecord(
                Id: reader.GetInt64(0),
                SourceGroupId: reader.GetString(1),
                TargetGroupId: reader.GetString(2),
                EventType: reader.GetString(3),
                Payload: reader.GetString(4),
                Status: reader.GetString(5),
                WorkspaceId: reader.GetString(6),
                ProjectId: await reader.IsDBNullAsync(7, ct).ConfigureAwait(false) ? null : reader.GetString(7),
                CreatedAt: reader.GetString(8),
                DeliveredAt: await reader.IsDBNullAsync(9, ct).ConfigureAwait(false) ? null : reader.GetString(9),
                AcknowledgedAt: await reader.IsDBNullAsync(10, ct).ConfigureAwait(false) ? null : reader.GetString(10)));
        }
        return results;
    }
}
