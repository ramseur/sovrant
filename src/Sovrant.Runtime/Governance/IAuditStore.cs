namespace Sovrant.Runtime.Governance;

/// <summary>
/// Abstraction for persisting audit events (governance verdicts, bash commands).
/// </summary>
public interface IAuditStore : IAsyncDisposable
{
    /// <summary>Logs a governance event (verdict) to the audit store.</summary>
    Task LogGovernanceEventAsync(
        GovernanceContext context,
        GovernanceVerdict verdict,
        CancellationToken ct = default);

    /// <summary>Logs a bash command execution to the audit store.</summary>
    Task LogBashCommandAsync(
        string command,
        string? sessionId,
        int exitCode,
        CancellationToken ct = default);
}
