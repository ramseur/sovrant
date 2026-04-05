using System.Globalization;
using System.Text.Json;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Append-only JSONL audit logger. Writes governance events to
/// <c>~/.sovrant/audit/governance.jsonl</c> and bash commands to
/// <c>~/.sovrant/audit/bash-commands.jsonl</c>.
/// </summary>
internal sealed class AuditLogger : IDisposable
{
    private static readonly string AuditDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "audit");

    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    /// <summary>Logs a governance event (verdict) to the governance audit log.</summary>
    internal async Task LogGovernanceEventAsync(
        GovernanceContext context,
        GovernanceVerdict verdict,
        CancellationToken ct = default)
    {
        if (_disposed) return;

        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            phase = context.Phase.ToString(),
            tool = context.ToolName,
            session_id = context.SessionId ?? "unknown",
            action = verdict.Action.ToString(),
            rule = verdict.Rule,
            reason = verdict.Reason,
        };

        await AppendAsync("governance.jsonl", entry, ct).ConfigureAwait(false);
    }

    /// <summary>Logs a bash command execution to the bash audit log.</summary>
    internal async Task LogBashCommandAsync(
        string command,
        string? sessionId,
        int exitCode,
        CancellationToken ct = default)
    {
        if (_disposed) return;

        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            command,
            session_id = sessionId ?? "unknown",
            exit_code = exitCode,
        };

        await AppendAsync("bash-commands.jsonl", entry, ct).ConfigureAwait(false);
    }

    private async Task AppendAsync(string fileName, object entry, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(AuditDir);
            var path = Path.Combine(AuditDir, fileName);
            await File.AppendAllTextAsync(path, line, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
    }
}
