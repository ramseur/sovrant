using Sovrant.Runtime.Conversation;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Clears in-memory conversation history to free context space.
/// (A future version will first ask the model to produce a summary that is re-injected.)
/// </summary>
public sealed class CompactCommand : ISlashCommand
{
    private readonly IConversationRuntime _runtime;
    private readonly TokenUsageTracker _tracker;

    public CompactCommand(IConversationRuntime runtime, TokenUsageTracker tracker)
    {
        _runtime = runtime;
        _tracker = tracker;
    }

    public string Name => "compact";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Clear in-memory history to free context space.";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        _runtime.Reset();
        _tracker.Reset();
        return Task.FromResult(new SlashCommandResult(
            "Conversation history compacted. Context window freed.",
            ShouldClearHistory: true));
    }
}
