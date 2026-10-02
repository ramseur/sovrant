namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Phase 133 — the conversation a tool call belongs to. Set by
/// <see cref="ConversationRuntime"/> for the duration of each turn, so tools
/// that start work (TeamRun, Swarm) can record which conversation launched it
/// without threading the session id through every signature. <c>null</c>
/// outside a conversation turn (Orchestration page, API, scheduler).
/// </summary>
public static class TurnContext
{
    private static readonly AsyncLocal<TurnInfo?> s_current = new();

    /// <summary>The turn bound to the current async flow, or <c>null</c>.</summary>
    public static TurnInfo? Current => s_current.Value;

    /// <summary>Binds <paramref name="sessionId"/> to the current async flow until the returned scope is disposed.</summary>
    public static IDisposable Begin(string sessionId, string? ownerUserId)
    {
        var previous = s_current.Value;
        s_current.Value = new TurnInfo(sessionId, ownerUserId);
        return new Restorer(previous);
    }

    private sealed class Restorer(TurnInfo? previous) : IDisposable
    {
        public void Dispose() => s_current.Value = previous;
    }
}

/// <summary>The conversation (and its owner) a turn is running in.</summary>
public sealed record TurnInfo(string SessionId, string? OwnerUserId);
