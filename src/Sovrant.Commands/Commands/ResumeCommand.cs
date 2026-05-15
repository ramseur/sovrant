using System.Globalization;
using System.Text;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Lists saved sessions or shows how to resume one.
/// Full session resume requires restarting with <c>--session &lt;id&gt;</c>.
/// </summary>
public sealed class ResumeCommand : ISlashCommand
{
    private readonly ISessionStore _store;
    private readonly IConversationRuntime _runtime;

    public ResumeCommand(ISessionStore store, IConversationRuntime runtime)
    {
        _store = store;
        _runtime = runtime;
    }

    public string Name => "resume";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "List saved sessions or show how to resume one.";
    public string Category => "Session";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var ids = await _store.ListAsync(ownerUserId: null, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(args))
        {
            if (ids.Count == 0)
                return new SlashCommandResult("No saved sessions to resume.");

            var sb = new StringBuilder();
            sb.AppendLine("Saved sessions:");
            foreach (var id in ids)
            {
                var marker = id == _runtime.SessionId ? " (current)" : string.Empty;
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {id}{marker}");
            }
            sb.Append("\nTo resume a session, run: sovrant --session <session-id>");
            return new SlashCommandResult(sb.ToString());
        }

        // A specific session ID was given — validate it exists
        if (!ids.Contains(args, StringComparer.Ordinal))
            return new SlashCommandResult(
                $"Session '{args}' not found. Use '/resume' to list available sessions.");

        return new SlashCommandResult(
            $"To resume session '{args}', restart with:\n  sovrant --session {args}");
    }
}
