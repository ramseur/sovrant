using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Decorator that writes audit events to both SQLite (primary) and JSONL (secondary).
/// Activated when <c>SOVRANT_AUDIT_JSONL=true</c>.
/// </summary>
internal sealed class DualWriteAuditStore(IAuditStore primary, IAuditStore secondary) : IAuditStore
{
    public async Task LogGovernanceEventAsync(
        GovernanceContext context,
        GovernanceVerdict verdict,
        CancellationToken ct = default)
    {
        await primary.LogGovernanceEventAsync(context, verdict, ct).ConfigureAwait(false);
        await secondary.LogGovernanceEventAsync(context, verdict, ct).ConfigureAwait(false);
    }

    public async Task LogBashCommandAsync(
        string command,
        string? sessionId,
        int exitCode,
        CancellationToken ct = default)
    {
        await primary.LogBashCommandAsync(command, sessionId, exitCode, ct).ConfigureAwait(false);
        await secondary.LogBashCommandAsync(command, sessionId, exitCode, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await primary.DisposeAsync().ConfigureAwait(false);
        await secondary.DisposeAsync().ConfigureAwait(false);
    }
}
