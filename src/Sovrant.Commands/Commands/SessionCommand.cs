using System.Globalization;
using System.Text;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;

namespace Sovrant.Commands.Commands;

/// <summary>Shows current session info and lists saved sessions.</summary>
public sealed class SessionCommand : ISlashCommand
{
    private readonly IConversationRuntime _runtime;
    private readonly ISessionStore _store;

    public SessionCommand(IConversationRuntime runtime, ISessionStore store)
    {
        _runtime = runtime;
        _store = store;
    }

    public string Name => "session";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Session management: list, delete <id>, purge.";
    public string Category => "Session";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (string.Equals(args, "list", StringComparison.OrdinalIgnoreCase))
            return await ListSessionsAsync(ct).ConfigureAwait(false);

        if (string.Equals(args, "purge", StringComparison.OrdinalIgnoreCase))
            return await PurgeSessionsAsync(ct).ConfigureAwait(false);

        if (args.StartsWith("delete ", StringComparison.OrdinalIgnoreCase))
            return await DeleteSessionAsync(args[7..].Trim(), ct).ConfigureAwait(false);

        var entries = await _store.LoadAsync(_runtime.SessionId, ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Session ID:  {_runtime.SessionId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Entries:     {entries.Count}");

        if (entries.Count > 0)
        {
            var first = entries[0];
            var last = entries[^1];
            sb.AppendLine(CultureInfo.InvariantCulture, $"Started:     {first.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Last entry:  {last.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        }

        sb.AppendLine();
        sb.Append("Use '/session list' to see all saved sessions.");

        return new SlashCommandResult(sb.ToString());
    }

    private async Task<SlashCommandResult> ListSessionsAsync(CancellationToken ct)
    {
        var ids = await _store.ListAsync(ct).ConfigureAwait(false);
        if (ids.Count == 0)
            return new SlashCommandResult("No saved sessions.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{ids.Count} session(s):");
        foreach (var id in ids)
        {
            var marker = id == _runtime.SessionId ? " (current)" : string.Empty;
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {id}{marker}");
        }

        sb.AppendLine("Use '/resume <session-id>' to resume a session.");
        sb.Append("Use '/session delete <id>' to delete, '/session purge' to delete all.");
        return new SlashCommandResult(sb.ToString());
    }

    private async Task<SlashCommandResult> DeleteSessionAsync(string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return new SlashCommandResult("Usage: /session delete <session-id>");

        if (sessionId == _runtime.SessionId)
            return new SlashCommandResult("Cannot delete the current session. Use /clear instead.");

        var deleted = await _store.DeleteAsync(sessionId, ct).ConfigureAwait(false);
        return deleted
            ? new SlashCommandResult(string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' deleted."))
            : new SlashCommandResult(string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' not found."));
    }

    private async Task<SlashCommandResult> PurgeSessionsAsync(CancellationToken ct)
    {
        var count = await _store.DeleteAllAsync(ct).ConfigureAwait(false);
        return new SlashCommandResult(string.Create(CultureInfo.InvariantCulture, $"{count} session(s) deleted."));
    }
}
