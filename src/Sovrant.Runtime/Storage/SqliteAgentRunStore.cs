using System.Globalization;
using System.Text;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 52 — SQLite-backed <see cref="IAgentRunStore"/>. Writes to
/// the <c>agent_runs</c> table created in V012.
/// </summary>
internal sealed class SqliteAgentRunStore(ISqliteConnectionFactory connectionFactory) : IAgentRunStore
{
    public async Task<AgentRunRecord> CreateAsync(AgentRunRecord run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO agent_runs
                (run_id, parent_run_id, team_id, member_id, workspace_id, project_id,
                 user_id, kind, status, started_at, input_tokens, output_tokens, cost_usd)
            VALUES
                ($runId, $parentRunId, $teamId, $memberId, $ws, $proj,
                 $userId, $kind, $status, $startedAt, $inTok, $outTok, $cost)
            """;
        cmd.Parameters.AddWithValue("$runId", run.RunId);
        cmd.Parameters.AddWithValue("$parentRunId", (object?)run.ParentRunId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$teamId", (object?)run.TeamId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$memberId", (object?)run.MemberId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ws", run.WorkspaceId);
        cmd.Parameters.AddWithValue("$proj", (object?)run.ProjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$userId", run.UserId);
        cmd.Parameters.AddWithValue("$kind", run.Kind);
        cmd.Parameters.AddWithValue("$status", run.Status);
        cmd.Parameters.AddWithValue("$startedAt", run.StartedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$inTok", run.InputTokens);
        cmd.Parameters.AddWithValue("$outTok", run.OutputTokens);
        AddDecimalParam(cmd, "$cost", run.CostUsd);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return run;
    }

    public async Task<AgentRunRecord?> GetAsync(string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(runId);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT run_id, parent_run_id, team_id, member_id, workspace_id, project_id,
                   user_id, kind, status, started_at, ended_at, input_tokens, output_tokens, cost_usd
            FROM agent_runs WHERE run_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", runId);
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false) ? ReadRecord(r) : null;
    }

    public async Task UpdateStatusAsync(string runId, string status, int inputTokens = 0, int outputTokens = 0, decimal? costUsd = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(runId);

        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE agent_runs
            SET status = $status,
                ended_at = $endedAt,
                input_tokens = input_tokens + $inTok,
                output_tokens = output_tokens + $outTok,
                cost_usd = CASE WHEN $cost IS NULL THEN cost_usd ELSE COALESCE(cost_usd, 0) + $cost END
            WHERE run_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", runId);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$endedAt", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$inTok", inputTokens);
        cmd.Parameters.AddWithValue("$outTok", outputTokens);
        AddDecimalParam(cmd, "$cost", costUsd);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentRunRecord>> ListAsync(AgentRunFilter? filter = null, int limit = 50, CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();

        var sb = new StringBuilder("""
            SELECT run_id, parent_run_id, team_id, member_id, workspace_id, project_id,
                   user_id, kind, status, started_at, ended_at, input_tokens, output_tokens, cost_usd
            FROM agent_runs WHERE 1=1
            """);

        if (filter?.WorkspaceId is not null)
        {
            sb.Append(" AND workspace_id = $ws");
            cmd.Parameters.AddWithValue("$ws", filter.WorkspaceId);
        }
        if (filter?.ProjectId is not null)
        {
            sb.Append(" AND project_id = $proj");
            cmd.Parameters.AddWithValue("$proj", filter.ProjectId);
        }
        if (filter?.UserId is not null)
        {
            sb.Append(" AND user_id = $userId");
            cmd.Parameters.AddWithValue("$userId", filter.UserId);
        }
        if (filter?.TeamId is not null)
        {
            sb.Append(" AND team_id = $teamId");
            cmd.Parameters.AddWithValue("$teamId", filter.TeamId);
        }
        if (filter?.Kind is not null)
        {
            sb.Append(" AND kind = $kind");
            cmd.Parameters.AddWithValue("$kind", filter.Kind);
        }
        if (filter?.Status is not null)
        {
            sb.Append(" AND status = $status");
            cmd.Parameters.AddWithValue("$status", filter.Status);
        }
        if (filter?.ParentRunId is not null)
        {
            sb.Append(" AND parent_run_id = $parentRunId");
            cmd.Parameters.AddWithValue("$parentRunId", filter.ParentRunId);
        }

        sb.Append(" ORDER BY started_at DESC LIMIT $limit");
        cmd.Parameters.AddWithValue("$limit", limit);

#pragma warning disable CA2100 // SQL is built from string literals only; values flow exclusively through parameters
        cmd.CommandText = sb.ToString();
#pragma warning restore CA2100

        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var list = new List<AgentRunRecord>();
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            list.Add(ReadRecord(r));
        return list;
    }

    private static AgentRunRecord ReadRecord(Microsoft.Data.Sqlite.SqliteDataReader r) => new(
        RunId: r.GetString(0),
        ParentRunId: r.IsDBNull(1) ? null : r.GetString(1),
        TeamId: r.IsDBNull(2) ? null : r.GetString(2),
        MemberId: r.IsDBNull(3) ? null : r.GetString(3),
        WorkspaceId: r.GetString(4),
        ProjectId: r.IsDBNull(5) ? null : r.GetString(5),
        UserId: r.GetString(6),
        Kind: r.GetString(7),
        Status: r.GetString(8),
        StartedAt: DateTimeOffset.Parse(r.GetString(9), CultureInfo.InvariantCulture),
        EndedAt: r.IsDBNull(10) ? null : DateTimeOffset.Parse(r.GetString(10), CultureInfo.InvariantCulture),
        InputTokens: r.GetInt32(11),
        OutputTokens: r.GetInt32(12),
        CostUsd: r.IsDBNull(13) ? null : (decimal)r.GetDouble(13));

    private static void AddDecimalParam(Microsoft.Data.Sqlite.SqliteCommand cmd, string name, decimal? value)
    {
        if (value.HasValue)
            cmd.Parameters.AddWithValue(name, (double)value.Value);
        else
            cmd.Parameters.AddWithValue(name, DBNull.Value);
    }
}
