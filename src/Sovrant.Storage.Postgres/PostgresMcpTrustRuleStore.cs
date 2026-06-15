using System.Globalization;
using Npgsql;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Storage.Postgres;

internal sealed class PostgresMcpTrustRuleStore(IPostgresConnectionFactory factory) : IMcpTrustRuleStore
{
    public async Task<IReadOnlyList<McpTrustRule>> GetRulesAsync(
        string workspaceId,
        string? serverName = null,
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();

        if (serverName is null)
        {
            cmd.CommandText =
                "SELECT rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at " +
                "FROM mcp_trust_rules WHERE workspace_id = $1 ORDER BY server_name, tool_pattern";
            cmd.Parameters.AddWithValue(workspaceId);
        }
        else
        {
            cmd.CommandText =
                "SELECT rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at " +
                "FROM mcp_trust_rules WHERE workspace_id = $1 AND server_name = $2 ORDER BY tool_pattern";
            cmd.Parameters.AddWithValue(workspaceId);
            cmd.Parameters.AddWithValue(serverName);
        }

        return await ReadRulesAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<McpTrustRule>> GetRulesForServerAsync(
        string serverName,
        CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at " +
            "FROM mcp_trust_rules WHERE server_name = $1 OR server_name = '*' " +
            "ORDER BY CASE WHEN server_name = '*' THEN 1 ELSE 0 END, tool_pattern";
        cmd.Parameters.AddWithValue(serverName);
        return await ReadRulesAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task UpsertAsync(McpTrustRule rule, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mcp_trust_rules
                (rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            ON CONFLICT (rule_id) DO UPDATE SET
                server_name  = EXCLUDED.server_name,
                tool_pattern = EXCLUDED.tool_pattern,
                action       = EXCLUDED.action,
                reason       = EXCLUDED.reason,
                updated_at   = EXCLUDED.updated_at
            """;

        cmd.Parameters.AddWithValue(rule.RuleId);
        cmd.Parameters.AddWithValue(rule.WorkspaceId);
        cmd.Parameters.AddWithValue(rule.ServerName);
        cmd.Parameters.AddWithValue(rule.ToolPattern);
        cmd.Parameters.AddWithValue(rule.Action.ToString());
        cmd.Parameters.AddWithValue((object?)rule.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue(rule.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue(rule.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string ruleId, string workspaceId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM mcp_trust_rules WHERE rule_id = $1 AND workspace_id = $2";
        cmd.Parameters.AddWithValue(ruleId);
        cmd.Parameters.AddWithValue(workspaceId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<McpTrustRule?> GetByIdAsync(string ruleId, string workspaceId, CancellationToken ct = default)
    {
        using var conn = factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at " +
            "FROM mcp_trust_rules WHERE rule_id = $1 AND workspace_id = $2";
        cmd.Parameters.AddWithValue(ruleId);
        cmd.Parameters.AddWithValue(workspaceId);

        var rules = await ReadRulesAsync(cmd, ct).ConfigureAwait(false);
        return rules.Count > 0 ? rules[0] : null;
    }

    private static async Task<IReadOnlyList<McpTrustRule>> ReadRulesAsync(
        NpgsqlCommand cmd,
        CancellationToken ct)
    {
        var results = new List<McpTrustRule>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var action = Enum.TryParse<McpTrustAction>(reader.GetString(4), ignoreCase: true, out var a)
                ? a : McpTrustAction.RequireConfirmation;
            var n5 = await reader.IsDBNullAsync(5, ct).ConfigureAwait(false);

            results.Add(new McpTrustRule(
                RuleId:      reader.GetString(0),
                WorkspaceId: reader.GetString(1),
                ServerName:  reader.GetString(2),
                ToolPattern: reader.GetString(3),
                Action:      action,
                Reason:      n5 ? null : reader.GetString(5),
                CreatedAt:   DateTimeOffset.Parse(reader.GetString(6), null, DateTimeStyles.RoundtripKind),
                UpdatedAt:   DateTimeOffset.Parse(reader.GetString(7), null, DateTimeStyles.RoundtripKind)));
        }
        return results;
    }
}
