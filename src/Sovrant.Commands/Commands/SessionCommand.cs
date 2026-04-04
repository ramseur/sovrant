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
    public string Description => "Show current session info. Use 'list' to see all sessions.";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        if (string.Equals(args, "list", StringComparison.OrdinalIgnoreCase))
            return await ListSessionsAsync(ct).ConfigureAwait(false);

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

        sb.Append("Use '/resume <session-id>' to resume a session.");
        return new SlashCommandResult(sb.ToString());
    }
}
