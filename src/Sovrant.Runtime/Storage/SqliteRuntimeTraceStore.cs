using System.Globalization;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 51 — SQLite-backed runtime trace store. Writes to the
/// <c>runtime_traces</c> table (V010). Opaque JSON payloads; the concrete
/// engine event shapes live in <c>Sovrant.Runtime.Engine</c>.
/// </summary>
internal sealed class SqliteRuntimeTraceStore(ISqliteConnectionFactory connectionFactory) : IRuntimeTraceStore
{
    public async Task AppendAsync(RuntimeTraceEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO runtime_traces
                (runtime_run_id, plan_id, plan_version, step_index, entry_type,
                 payload, timestamp, session_id, workspace_id, project_id)
            VALUES
                ($runtimeRunId, $planId, $planVersion, $stepIndex, $entryType,
                 $payload, $timestamp, $sessionId, $workspaceId, $projectId)
            """;
        cmd.Parameters.AddWithValue("$runtimeRunId", entry.RuntimeRunId);
        cmd.Parameters.AddWithValue("$planId", entry.PlanId);
        cmd.Parameters.AddWithValue("$planVersion", entry.PlanVersion);
        cmd.Parameters.AddWithValue("$stepIndex", entry.StepIndex.HasValue ? entry.StepIndex.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$entryType", entry.EntryType);
        cmd.Parameters.AddWithValue("$payload", entry.Payload);
        cmd.Parameters.AddWithValue(
            "$timestamp",
            entry.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$sessionId", (object?)entry.SessionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$workspaceId", (object?)entry.WorkspaceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$projectId", (object?)entry.ProjectId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RuntimeTraceEntry>> LoadAsync(string runtimeRunId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeRunId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT runtime_run_id, plan_id, plan_version, step_index, entry_type,
                   payload, timestamp, session_id, workspace_id, project_id
            FROM runtime_traces
            WHERE runtime_run_id = $runtimeRunId
            ORDER BY id ASC
            """;
        cmd.Parameters.AddWithValue("$runtimeRunId", runtimeRunId);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<RuntimeTraceEntry>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new RuntimeTraceEntry(
                RuntimeRunId: reader.GetString(0),
                PlanId: reader.GetString(1),
                PlanVersion: reader.GetInt32(2),
                StepIndex: await reader.IsDBNullAsync(3, ct).ConfigureAwait(false) ? null : reader.GetInt32(3),
                EntryType: reader.GetString(4),
                Payload: reader.GetString(5),
                Timestamp: DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
                SessionId: await reader.IsDBNullAsync(7, ct).ConfigureAwait(false) ? null : reader.GetString(7),
                WorkspaceId: await reader.IsDBNullAsync(8, ct).ConfigureAwait(false) ? null : reader.GetString(8),
                ProjectId: await reader.IsDBNullAsync(9, ct).ConfigureAwait(false) ? null : reader.GetString(9)));
        }
        return results;
    }

    public async Task<IReadOnlyList<string>> ListInFlightRunsAsync(CancellationToken ct = default)
    {
        // "In flight" = the run has at least one step_started entry whose
        // (plan_id, plan_version, step_index) does NOT have a matching
        // step_completed or step_failed entry. We compute this with a single
        // GROUP BY over the trace table.
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT started.runtime_run_id
            FROM runtime_traces AS started
            WHERE started.entry_type = 'step_started'
              AND NOT EXISTS (
                  SELECT 1
                  FROM runtime_traces AS finished
                  WHERE finished.runtime_run_id = started.runtime_run_id
                    AND finished.plan_id        = started.plan_id
                    AND finished.plan_version   = started.plan_version
                    AND finished.step_index     = started.step_index
                    AND finished.entry_type IN ('step_completed', 'step_failed')
              )
            """;

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<string>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }

    public async Task<int> DeleteRunAsync(string runtimeRunId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeRunId);

        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM runtime_traces WHERE runtime_run_id = $runtimeRunId";
        cmd.Parameters.AddWithValue("$runtimeRunId", runtimeRunId);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
