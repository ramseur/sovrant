using System.Globalization;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 51 — SQLite-backed <see cref="IWorkflowStore"/>. Writes to the
/// V011 <c>workflows</c> and <c>workflow_events</c> tables. State updates
/// on the <c>workflows</c> row are a cache; the append-only
/// <c>workflow_events</c> journal is the canonical history.
/// </summary>
internal sealed class SqliteWorkflowStore(ISqliteConnectionFactory connectionFactory) : IWorkflowStore
{
    public async Task<Workflow> CreateAsync(
        string goal,
        string? sessionId = null,
        string? workspaceId = null,
        string? projectId = null,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        var id = $"workflow-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var nowText = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        using var connection = connectionFactory.CreateConnection();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO workflows
                    (id, goal, status, plan_json, created_at, updated_at,
                     session_id, workspace_id, project_id, owner_user_id, is_private)
                VALUES
                    ($id, $goal, 'planning', '{}', $now, $now,
                     $sessionId, $workspaceId, $projectId, $ownerUserId, 1)
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$goal", goal);
            cmd.Parameters.AddWithValue("$now", nowText);
            cmd.Parameters.AddWithValue("$sessionId", (object?)sessionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$workspaceId", (object?)workspaceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ownerUserId", (object?)ownerUserId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // Write the canonical "mission_created" journal entry.
        using (var evt = connection.CreateCommand())
        {
            evt.CommandText = """
                INSERT INTO workflow_events
                    (workflow_id, event_type, payload, timestamp, workspace_id, project_id)
                VALUES
                    ($workflowId, 'mission_created', $payload, $now, $workspaceId, $projectId)
                """;
            evt.Parameters.AddWithValue("$workflowId", id);
            evt.Parameters.AddWithValue("$payload",
                $$"""{"goal":{{System.Text.Json.JsonSerializer.Serialize(goal)}}}""");
            evt.Parameters.AddWithValue("$now", nowText);
            evt.Parameters.AddWithValue("$workspaceId", (object?)workspaceId ?? DBNull.Value);
            evt.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
            await evt.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return new Workflow(
            Id: id,
            Goal: goal,
            Status: WorkflowStatus.Planning,
            PlanJson: "{}",
            CreatedAt: now,
            UpdatedAt: now,
            CompletedAt: null,
            SessionId: sessionId,
            WorkspaceId: workspaceId,
            ProjectId: projectId,
            OwnerUserId: ownerUserId,
            IsPrivate: true);
    }

    public async Task<Workflow?> GetAsync(string workflowId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, goal, status, plan_json, created_at, updated_at, completed_at,
                   session_id, workspace_id, project_id, owner_user_id, is_private
            FROM workflows
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", workflowId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return ReadWorkflow(reader);
    }

    public async Task<IReadOnlyList<Workflow>> ListAsync(
        string? ownerUserId = null,
        WorkflowStatus? status = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();

        // Compose the WHERE clause from fixed fragments — no interpolation
        // of user input into SQL text.
        if (ownerUserId is not null && status is not null)
        {
            cmd.CommandText = """
                SELECT id, goal, status, plan_json, created_at, updated_at, completed_at,
                       session_id, workspace_id, project_id, owner_user_id, is_private
                FROM workflows
                WHERE owner_user_id = $owner AND status = $status
                ORDER BY created_at DESC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$owner", ownerUserId);
            cmd.Parameters.AddWithValue("$status", StatusToString(status.Value));
        }
        else if (ownerUserId is not null)
        {
            cmd.CommandText = """
                SELECT id, goal, status, plan_json, created_at, updated_at, completed_at,
                       session_id, workspace_id, project_id, owner_user_id, is_private
                FROM workflows
                WHERE owner_user_id = $owner
                ORDER BY created_at DESC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$owner", ownerUserId);
        }
        else if (status is not null)
        {
            cmd.CommandText = """
                SELECT id, goal, status, plan_json, created_at, updated_at, completed_at,
                       session_id, workspace_id, project_id, owner_user_id, is_private
                FROM workflows
                WHERE status = $status
                ORDER BY created_at DESC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$status", StatusToString(status.Value));
        }
        else
        {
            cmd.CommandText = """
                SELECT id, goal, status, plan_json, created_at, updated_at, completed_at,
                       session_id, workspace_id, project_id, owner_user_id, is_private
                FROM workflows
                ORDER BY created_at DESC
                LIMIT $limit
                """;
        }
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<Workflow>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            results.Add(ReadWorkflow(reader));
        return results;
    }

    public async Task UpdateStateAsync(
        string workflowId,
        WorkflowStatus status,
        string? planJson = null,
        DateTimeOffset? completedAt = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowId);

        var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE workflows
            SET status       = $status,
                plan_json    = COALESCE($planJson, plan_json),
                completed_at = COALESCE($completedAt, completed_at),
                updated_at   = $now
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", workflowId);
        cmd.Parameters.AddWithValue("$status", StatusToString(status));
        cmd.Parameters.AddWithValue("$planJson", (object?)planJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$completedAt",
            completedAt is null
                ? DBNull.Value
                : completedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$now", now);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<WorkflowEvent> AppendEventAsync(
        string workflowId,
        string eventType,
        string payloadJson,
        string? workspaceId = null,
        string? projectId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowId);
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(payloadJson);

        var now = DateTimeOffset.UtcNow;
        var nowText = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO workflow_events
                (workflow_id, event_type, payload, timestamp, workspace_id, project_id)
            VALUES
                ($workflowId, $eventType, $payload, $now, $workspaceId, $projectId);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$workflowId", workflowId);
        cmd.Parameters.AddWithValue("$eventType", eventType);
        cmd.Parameters.AddWithValue("$payload", payloadJson);
        cmd.Parameters.AddWithValue("$now", nowText);
        cmd.Parameters.AddWithValue("$workspaceId", (object?)workspaceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$projectId", (object?)projectId ?? DBNull.Value);
        var idObj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var id = Convert.ToInt64(idObj, CultureInfo.InvariantCulture);

        return new WorkflowEvent(id, workflowId, eventType, payloadJson, now, workspaceId, projectId);
    }

    public async Task UpdatePrivacyAsync(
        string workflowId,
        string ownerUserId,
        bool isPrivate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowId);
        ArgumentNullException.ThrowIfNull(ownerUserId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE workflows SET is_private = $v WHERE id = $id AND owner_user_id = $owner";
        cmd.Parameters.AddWithValue("$v", isPrivate ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", workflowId);
        cmd.Parameters.AddWithValue("$owner", ownerUserId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkflowEvent>> GetEventsAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, workflow_id, event_type, payload, timestamp, workspace_id, project_id
            FROM workflow_events
            WHERE workflow_id = $workflowId
            ORDER BY id ASC
            """;
        cmd.Parameters.AddWithValue("$workflowId", workflowId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<WorkflowEvent>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new WorkflowEvent(
                Id: reader.GetInt64(0),
                WorkflowId: reader.GetString(1),
                EventType: reader.GetString(2),
                PayloadJson: reader.GetString(3),
                Timestamp: DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                WorkspaceId: await reader.IsDBNullAsync(5, ct).ConfigureAwait(false) ? null : reader.GetString(5),
                ProjectId: await reader.IsDBNullAsync(6, ct).ConfigureAwait(false) ? null : reader.GetString(6)));
        }
        return results;
    }

    private static Workflow ReadWorkflow(System.Data.Common.DbDataReader r)
    {
        return new Workflow(
            Id: r.GetString(0),
            Goal: r.GetString(1),
            Status: ParseStatus(r.GetString(2)),
            PlanJson: r.GetString(3),
            CreatedAt: DateTimeOffset.Parse(r.GetString(4), CultureInfo.InvariantCulture),
            UpdatedAt: DateTimeOffset.Parse(r.GetString(5), CultureInfo.InvariantCulture),
            CompletedAt: r.IsDBNull(6) ? null : DateTimeOffset.Parse(r.GetString(6), CultureInfo.InvariantCulture),
            SessionId: r.IsDBNull(7) ? null : r.GetString(7),
            WorkspaceId: r.IsDBNull(8) ? null : r.GetString(8),
            ProjectId: r.IsDBNull(9) ? null : r.GetString(9),
            OwnerUserId: r.IsDBNull(10) ? null : r.GetString(10),
            IsPrivate: !r.IsDBNull(11) && r.GetInt64(11) != 0);
    }

    private static string StatusToString(WorkflowStatus s) => s switch
    {
        WorkflowStatus.Planning => "planning",
        WorkflowStatus.Running => "running",
        WorkflowStatus.AwaitingHuman => "awaiting_human",
        WorkflowStatus.Completed => "completed",
        WorkflowStatus.Failed => "failed",
        WorkflowStatus.Cancelled => "cancelled",
        _ => "planning",
    };

    private static WorkflowStatus ParseStatus(string s) => s switch
    {
        "planning" => WorkflowStatus.Planning,
        "running" => WorkflowStatus.Running,
        "awaiting_human" => WorkflowStatus.AwaitingHuman,
        "completed" => WorkflowStatus.Completed,
        "failed" => WorkflowStatus.Failed,
        "cancelled" => WorkflowStatus.Cancelled,
        _ => WorkflowStatus.Planning,
    };
}
