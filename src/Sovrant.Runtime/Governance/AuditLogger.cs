using System.Globalization;
using System.Text.Json;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Append-only JSONL audit logger. Writes governance events to
/// <c>~/.sovrant/audit/governance.jsonl</c> and bash commands to
/// <c>~/.sovrant/audit/bash-commands.jsonl</c>.
/// </summary>
internal sealed class AuditLogger : IAuditStore, IDisposable
{
    private static readonly string AuditDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", "audit");

    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    /// <inheritdoc />
    public async Task LogGovernanceEventAsync(
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

    /// <inheritdoc />
    public async Task LogBashCommandAsync(
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

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
