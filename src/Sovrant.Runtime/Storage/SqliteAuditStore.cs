using System.Globalization;
using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// SQLite-backed audit store. Writes governance events to <c>audit_governance</c>
/// and bash commands to <c>audit_bash</c>.
/// </summary>
internal sealed class SqliteAuditStore(ISqliteConnectionFactory connectionFactory) : IAuditStore
{
    public async Task LogGovernanceEventAsync(
        GovernanceContext context,
        GovernanceVerdict verdict,
        CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO audit_governance (timestamp, phase, tool, session_id, action, rule, reason)
            VALUES ($ts, $phase, $tool, $sid, $action, $rule, $reason)
            """;
        cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$phase", context.Phase.ToString());
        cmd.Parameters.AddWithValue("$tool", context.ToolName);
        cmd.Parameters.AddWithValue("$sid", context.SessionId ?? "unknown");
        cmd.Parameters.AddWithValue("$action", verdict.Action.ToString());
        cmd.Parameters.AddWithValue("$rule", verdict.Rule);
        cmd.Parameters.AddWithValue("$reason", verdict.Reason);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task LogBashCommandAsync(
        string command,
        string? sessionId,
        int exitCode,
        CancellationToken ct = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO audit_bash (timestamp, command, session_id, exit_code)
            VALUES ($ts, $cmd, $sid, $exit)
            """;
        cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$cmd", command);
        cmd.Parameters.AddWithValue("$sid", sessionId ?? "unknown");
        cmd.Parameters.AddWithValue("$exit", exitCode);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
