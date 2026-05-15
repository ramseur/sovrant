using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Session;

namespace Sovrant.Commands.Commands;

/// <summary>Renames the current session.</summary>
public sealed class RenameCommand : ISlashCommand
{
    private readonly IConversationRuntime _runtime;
    private readonly ISessionStore _store;

    public RenameCommand(IConversationRuntime runtime, ISessionStore store)
    {
        _runtime = runtime;
        _store = store;
    }

    public string Name => "rename";
    public IReadOnlyList<string> Aliases => ["title"];
    public string Description => "Rename the current session. Usage: /rename My Session Title";
    public string Category => "Session";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var title = args.Trim();
        if (string.IsNullOrEmpty(title))
            return new SlashCommandResult("Usage: /rename <title>");

        await _store.SetTitleAsync(_runtime.SessionId, title, ct: ct).ConfigureAwait(false);
        return new SlashCommandResult($"Session renamed to \"{title}\".");
    }
}
