using Sovrant.Runtime.Conversation;

namespace Sovrant.Commands.Commands;

/// <summary>Clears the conversation history for the current session.</summary>
public sealed class ClearCommand : ISlashCommand
{
    private readonly IConversationRuntime _runtime;
    private readonly TokenUsageTracker _tracker;

    public ClearCommand(IConversationRuntime runtime, TokenUsageTracker tracker)
    {
        _runtime = runtime;
        _tracker = tracker;
    }

    public string Name => "clear";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Clear conversation history.";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        _runtime.Reset();
        _tracker.Reset();
        return Task.FromResult(new SlashCommandResult(
            "Conversation history cleared.",
            ShouldClearHistory: true));
    }
}
