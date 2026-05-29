using System.Globalization;
using Microsoft.Data.Sqlite;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Storage;

/// <summary>SQLite-backed store for MCP trust gate rules.</summary>
internal sealed class SqliteMcpTrustRuleStore(ISqliteConnectionFactory connectionFactory) : IMcpTrustRuleStore
{
    public async Task<IReadOnlyList<McpTrustRule>> GetRulesAsync(
        string workspaceId,
        string? serverName = null,
        CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();

        if (serverName is null)
        {
            cmd.CommandText =
                "SELECT rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at " +
                "FROM mcp_trust_rules WHERE workspace_id = $wid ORDER BY server_name, tool_pattern";
            cmd.Parameters.AddWithValue("$wid", workspaceId);
        }
        else
        {
            cmd.CommandText =
                "SELECT rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at " +
                "FROM mcp_trust_rules WHERE workspace_id = $wid AND server_name = $srv ORDER BY tool_pattern";
            cmd.Parameters.AddWithValue("$wid", workspaceId);
            cmd.Parameters.AddWithValue("$srv", serverName);
        }

        return await ReadRulesAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<McpTrustRule>> GetRulesForServerAsync(
        string serverName,
        CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at " +
            "FROM mcp_trust_rules WHERE server_name = $srv OR server_name = '*' " +
            "ORDER BY CASE WHEN server_name = '*' THEN 1 ELSE 0 END, tool_pattern";
        cmd.Parameters.AddWithValue("$srv", serverName);

        return await ReadRulesAsync(cmd, ct).ConfigureAwait(false);
    }

    public async Task UpsertAsync(McpTrustRule rule, CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mcp_trust_rules (rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at)
            VALUES ($id, $wid, $srv, $pat, $act, $reason, $cat, $uat)
            ON CONFLICT(rule_id) DO UPDATE SET
                server_name  = excluded.server_name,
                tool_pattern = excluded.tool_pattern,
                action       = excluded.action,
                reason       = excluded.reason,
                updated_at   = excluded.updated_at
            """;

        cmd.Parameters.AddWithValue("$id", rule.RuleId);
        cmd.Parameters.AddWithValue("$wid", rule.WorkspaceId);
        cmd.Parameters.AddWithValue("$srv", rule.ServerName);
        cmd.Parameters.AddWithValue("$pat", rule.ToolPattern);
        cmd.Parameters.AddWithValue("$act", rule.Action.ToString());
        cmd.Parameters.AddWithValue("$reason", (object?)rule.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cat", rule.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$uat", rule.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string ruleId, string workspaceId, CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM mcp_trust_rules WHERE rule_id = $id AND workspace_id = $wid";
        cmd.Parameters.AddWithValue("$id", ruleId);
        cmd.Parameters.AddWithValue("$wid", workspaceId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<McpTrustRule?> GetByIdAsync(string ruleId, string workspaceId, CancellationToken ct = default)
    {
        using var conn = connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT rule_id, workspace_id, server_name, tool_pattern, action, reason, created_at, updated_at " +
            "FROM mcp_trust_rules WHERE rule_id = $id AND workspace_id = $wid";
        cmd.Parameters.AddWithValue("$id", ruleId);
        cmd.Parameters.AddWithValue("$wid", workspaceId);

        var rules = await ReadRulesAsync(cmd, ct).ConfigureAwait(false);
        return rules.Count > 0 ? rules[0] : null;
    }

    private static async Task<IReadOnlyList<McpTrustRule>> ReadRulesAsync(
        SqliteCommand cmd,
        CancellationToken ct)
    {
        var results = new List<McpTrustRule>();
        var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using var _ = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var action = Enum.TryParse<McpTrustAction>(reader.GetString(4), ignoreCase: true, out var a)
                ? a : McpTrustAction.RequireConfirmation;

            var isNull5 = await reader.IsDBNullAsync(5, ct).ConfigureAwait(false);
            results.Add(new McpTrustRule(
                RuleId: reader.GetString(0),
                WorkspaceId: reader.GetString(1),
                ServerName: reader.GetString(2),
                ToolPattern: reader.GetString(3),
                Action: action,
                Reason: isNull5 ? null : reader.GetString(5),
                CreatedAt: DateTimeOffset.Parse(reader.GetString(6), null, DateTimeStyles.RoundtripKind),
                UpdatedAt: DateTimeOffset.Parse(reader.GetString(7), null, DateTimeStyles.RoundtripKind)));
        }
        return results;
    }
}
