namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Carries the active <see cref="SessionConfig"/> through the async flow so any
/// runtime component (permissions, tool gating, …) can resolve per-session
/// state without threading it through every signature.
///
/// The hosting layer (server, desktop) populates this before invoking
/// <see cref="IConversationRuntime.RunTurnAsync(string, System.Threading.CancellationToken)"/>;
/// the runtime reads it during the turn loop.
/// </summary>
public static class SessionContext
{
    private static readonly AsyncLocal<SessionConfig?> s_current = new();

    /// <summary>The session config bound to the current async flow, or <c>null</c>.</summary>
    public static SessionConfig? Current
    {
        get => s_current.Value;
        set => s_current.Value = value;
    }

    /// <summary>
    /// Sets <see cref="Current"/> to <paramref name="config"/> and returns a
    /// disposable that restores the previous value. Use with <c>using</c>
    /// to scope a session config to a single async block.
    /// </summary>
    public static IDisposable Push(SessionConfig? config)
    {
        var previous = s_current.Value;
        s_current.Value = config;
        return new Restorer(previous);
    }

    private sealed class Restorer(SessionConfig? previous) : IDisposable
    {
        public void Dispose() => s_current.Value = previous;
    }
}
