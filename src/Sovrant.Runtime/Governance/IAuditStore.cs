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

    /// <summary>
    /// Phase 99 — logs a privacy-toggle event. Records who flipped the flag,
    /// which entity (session/mission/agent_run), the entity id, and the new
    /// value. Stored in <c>audit_governance</c> with rule "privacy_changed"
    /// so existing audit-export pipelines pick it up automatically.
    /// </summary>
    Task LogPrivacyChangeAsync(
        string userId,
        string entityKind,
        string entityId,
        bool newIsPrivate,
        CancellationToken ct = default);
}
